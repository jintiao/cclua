using System;
using System.Text;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lstring {

            /*
            ** Lua will use at most ~(2^LUAI_HASHLIMIT) bytes from a string to
            ** compute its hash
            */
            public const int LUAI_HASHLIMIT = 5;

            public static bool memcmp (byte[] a1, int offset, byte[] a2, int l) {
				for (int i = 0; i < l; i++) {
                    if (a1[i + offset] != a2[i])
						return false;
				}
				return true;
			}
            public static bool memcmp (byte[] a1, byte[] a2, int l) { return memcmp (a1, 0, a2, l); }

            /*
            ** creates a new string object
            */
            public static TString createstrobj (lua_State L, byte[] str, int offset, int l, int tag, uint h) {
                TString ts = luaC_newobj<TString> (L, tag);
                ts.len = l;
                ts.hash = h;
                ts.extra = 0;
                ts.data = str;
                ts.offset = offset;
                return ts;
            }
            public static TString createstrobj (lua_State L, byte[] str, int l, int tag, uint h) { return createstrobj (L, str, 0, l, tag, h); }

			/*
			** checks whether short string exists and reuses it or creates a new one
			*/
			public static TString internshrstr (lua_State L, byte[] str, int offset, int l) {
				global_State g = G (L);
                uint h = luaS_hash (str, offset, l, g.seed);
                long mod = lmod (h, g.strt.size);
                TString list = g.strt.hash[mod];
				TString ts = list;
				for (; ts != null; ts = ts.hnext) {
                    if (l == ts.len && memcmp (str, offset, ts.data, l) == true) {
						/* found! */
						if (isdead (g, ts))  /* dead (but not collected yet)? */
							changewhite (ts);  /* resurrect it */
						return ts;
					}
				}
				if (g.strt.nuse >= g.strt.size && g.strt.size <= MAX_INT / 2) {
					luaS_resize (L, g.strt.size * 2);
                    mod = lmod (h, g.strt.size);
                    list = g.strt.hash[mod];  /* recompute with new size */
				}
                ts = createstrobj (L, str, offset, l, LUA_TSHRSTR, h);
				ts.hnext = list;
                g.strt.hash[mod] = ts;
				g.strt.nuse++;
				return ts;
			}
			public static TString internshrstr (lua_State L, byte[] str, int l) { return internshrstr(L, str, 0, l); }
		}


		public static TString luaS_newliteral (lua_State L, string str) { return luaS_new (L, str); }


		/*
		** test whether a string is a reserved word
		*/
		public static bool isreserved (TString s) { return ((s.tt == LUA_TSHRSTR) && (s.extra > 0)); }


        /*
        ** equality for short strings, which are always internalized
        */
        public static bool eqshrstr (TString a, TString b) { return check_exp<bool> (a.tt == LUA_TSHRSTR, (a == b)); }


		/*
		** equality for long strings
		*/
		public static bool luaS_eqlngstr (TString a, TString b) {
			lua_assert (a.tt == LUA_TLNGSTR && b.tt == LUA_TLNGSTR);
			return ((a == b) ||  /* same instance or... */
			        ((a.len == b.len) &&  /* equal length and ... */
                    lstring.memcmp (a.data, b.data, a.len)));  /* equal contents */
		}


		public static uint luaS_hash (byte[] str, int offset, int l, uint seed) {
			uint h = seed ^ (uint)l;
			int step = (1 >> lstring.LUAI_HASHLIMIT) + 1;
			for (int l1 = l; l1 >= step; l1 -= step)
                h = h ^ ((h << 5) + (h >> 2) + str[l1 - 1 + offset]);
			return h;
		}
        public static uint luaS_hash (byte[] str, int l, uint seed) { return luaS_hash (str, 0, l, seed); }


        /*
        ** resizes the string table
        */
        public static void luaS_resize (lua_State L, long newsize) {
			stringtable tb = G (L).strt;

            if (newsize == tb.size)
                return;

			if (newsize > tb.size) {  /* grow table if needed */
				luaM_reallocvector<TString> (L, ref tb.hash, tb.size, newsize);
                for (long i = tb.size; i < newsize; i++)
					tb.hash[i] = null;
			}
            for (long i = 0; i < tb.size; i++) {  /* rehash */
                TString p = tb.hash[i];
                tb.hash[i] = null;
                while (p != null) {  /* for each node in the list */
                    TString hnext = p.hnext;  /* save next */
                    long h = lmod (p.hash, newsize);  /* new position */
                    p.hnext = tb.hash[h];  /* chain it */
                    tb.hash[h] = p;
                    p = hnext;
                }
            }
			if (newsize < tb.size) {  /* shrink table if needed */
				/* vanishing slice should be empty */
				lua_assert (tb.hash[newsize] == null && tb.hash[tb.size - 1] == null);
				luaM_reallocvector<TString> (L, ref tb.hash, tb.size, newsize);
			}
			tb.size = newsize;
        }



		public static void luaS_remove (lua_State L, TString ts) {
			stringtable tb = G (L).strt;
            long mod = lmod (ts.hash, tb.size);
			TString p = tb.hash[mod];
            if (p == ts) {
                tb.hash[mod] = p.hnext;
            }
            else {
                TString last = null;
                while (p != ts) {  /* find previous element */
                    last = p;
                    p = p.hnext;
                }
                last.hnext = p.hnext;  /* remove element from its list */
            }
			tb.nuse--;
		}

		/*
		** new string (with explicit length)
		*/
		public static TString luaS_newlstr (lua_State L, byte[] str, int offset, int len) {
			if (str.Length <= LUAI_MAXSHORTLEN)  /* short string? */
                return lstring.internshrstr (L, str, offset, len);
			else {
                if ((len) > MAX_SIZE)
					luaM_toobig (L);
                return lstring.createstrobj (L, str, offset, len, LUA_TLNGSTR, G (L).seed);
			}
		}
        public static TString luaS_newlstr (lua_State L, byte[] str, int len) { return luaS_newlstr (L, str, 0, len); }

		/*
		** new zero-terminated string
		*/
        public static TString luaS_new (lua_State L, string str) {
            byte[] buf = str2byte (str);
            return luaS_newlstr (L, buf, buf.Length);
		}


        public static byte[] str2byte (string str) { return Encoding.UTF8.GetBytes (str); }
        public static string byte2str (byte[] buf) { return Encoding.UTF8.GetString (buf); }
        public static string byte2str (byte[] buf, int index, int count) { return Encoding.UTF8.GetString (buf, index, count); }
    }
}
