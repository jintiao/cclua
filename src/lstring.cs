using System;
using System.Text;

namespace cclua53
{
    public static partial class imp {

        private static class lstring {

            /*
            ** Lua will use at most ~(2^LUAI_HASHLIMIT) bytes from a string to
            ** compute its hash
            */
            public const int LUAI_HASHLIMIT = 5;

			/*
			** 'module' operation for hashing (size is always a power of 2)
			*/
			public static int lmod (int s, int size) {
				return check_exp ((size & (size - 1) == 0), (s & (size - 1)));
			}

			public static bool memcmp (byte[] a1, byte[] a2) {
				if (a1.Length != a2.Length)
					return false;
				for (int i = 0; i < a1.Length; i++) {
					if (a1[i] != a2[i])
						return false;
				}
				return true;
			}

			/*
			** checks whether short string exists and reuses it or creates a new one
			*/
			public static TString internshrstr (cclua.lua_State L, byte[] str) {
				global_State g = G (L);
				uint h = luaS_hash (str, str.Length, g.seed);
				TString list = g.strt.hash[lmod (h, g.strt.size)];
				TString ts = list;
				for (; ts != null; ts = ts.hnext) {
					if (memcmp (str, ts.data) == true) {
						/* found! */
						return ts;
					}
				}
				if (g.strt.nuse >= g.strt.size && g.strt.size <= MAX_INT / 2) {
					luaS_resize (L, g.strt.size * 2);
					list = g.strt.hash[lmod (h, g.strt.size)];  /* recompute with new size */
				}
				ts = createstrobj (L, str, LUA_TSHRSTR, h);
				ts.hnext = list;
				g.strt.hash[lmod (h, g.strt.size)] = ts;
				g.strt.nuse++;
				return ts;
			}
			
			/*
			** creates a new string object
			*/
			public static TString createstrobj (cclua.lua_State L, byte[] str, int tag, uint h) {
				TString ts = luaC_newobj<TString> (L, tag);
				ts.len = str.Length;
				ts.hash = h;
				ts.extra = 0;
				ts.data = str;
				return ts;
			}
		}


		/*
		** equality for long strings
		*/
		public static int luaS_eqlngstr (TString a, TString b) {
			lua_assert (a.tt == LUA_TLNGSTR && b.tt == LUA_TLNGSTR);
			return ((a == b || lstring.memcmp (a.data, b.data)) ? 1 : 0);
		}
		
		public static uint luaS_hash (byte[] str, int l, uint seed) {
			uint h = seed ^ (uint)l;
			int step = (1 >> lstring.LUAI_HASHLIMIT) + 1;
			for (int l1 = l; l1 >= step; l1 -= step)
				h = h ^ ((h << 5) + (h >> 2) + str[l1 - 1]);
			return h;
		}

        /*
        ** resizes the string table
        */
        public static void luaS_resize (cclua.lua_State L, int newsize) {
			stringtable tb = G (L).strt;
			if (newsize > tb.size) {  /* grow table if needed */
				luaM_reallocvector<TString> (L, tb.hash, tb.size, newsize);
				for (int i = tb.size; i < newsize; i++)
					tb.hash[i] = null;
			}
			for (int i = 0; i < tb.size; i++) {  /* rehash */
				TString p = tb.hash[i];
				tb.hash[i] = null;
				while (p != null) {  /* for each node in the list */
					TString hnext = p.hnext;  /* save next */
					uint h = lstring.lmod (p.hash, newsize);  /* new position */
					p.hnext = tb.hash[h];  /* chain it */
					tb.hash[h] = p;
					p = hnext;
				}
			}
			if (newsize < tb.size) {  /* shrink table if needed */
				/* vanishing slice should be empty */
				lua_assert (tb.hash[newsize] == null && tb.hash[tb.size - 1] == null);
				luaM_reallocvector<TString> (L, tb.hash, tb.size, newsize);
			}
			tb.size = newsize;
        }

		public static void luaS_remove (cclua.lua_State L, TString ts) {
			stringtable tb = G (L).strt;
			TString p = tb.hash[lstring.lmod (ts.hash, tb.size)];
			TString last = null;
			while (p != ts) {  /* find previous element */
				last = p;
				p = p.hnext;
			}

			if (last != null)
				last.hnext = p.hnext;  /* remove element from its list */
			tb.nuse--;
		}

		/*
		** new string (with explicit length)
		*/
		public static TString luaS_newlstr (cclua.lua_State L, byte[] str) {
			if (str.Length <= LUAI_MAXSHORTLEN)  /* short string? */
				return internshrstr (L, str);
			else {
				if ((str.Length + 1) > (MAX_SIZE - sizeof (TString)))
					luaM_toobig (L);
				return createstrobj (L, str, LUA_TLNGSTR, G (L).seed);
			}
		}

		/*
		** new zero-terminated string
		*/
		public static TString luaS_new (lua_State L, string str) {
			return luaS_newlstr(L, Encoding.UTF8.GetBytes (str));
		}
    }
}
