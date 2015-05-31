﻿using System;
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

			public static bool memcmp (byte[] a1, byte[] a2) {
				for (int i = 0; i < a1.Length; i++) {
					if (a1[i] != a2[i])
						return false;
				}
				return true;
			}

            /*
            ** creates a new string object
            */
            public static TString createstrobj (lua_State L, byte[] str, int tag, uint h) {
                TString ts = luaC_newobj<TString> (L, tag);
                ts.len = str.Length;
                ts.hash = h;
                ts.extra = 0;
                ts.data = str;
                return ts;
            }

			/*
			** checks whether short string exists and reuses it or creates a new one
			*/
			public static TString internshrstr (lua_State L, byte[] str) {
				global_State g = G (L);
				uint h = luaS_hash (str, str.Length, g.seed);
                long mod = lmod (h, g.strt.size);
                TString list = g.strt.hash[mod];
				TString ts = list;
				for (; ts != null; ts = ts.hnext) {
					if (memcmp (str, ts.data) == true) {
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
				ts = createstrobj (L, str, LUA_TSHRSTR, h);
				ts.hnext = list;
                g.strt.hash[mod] = ts;
				g.strt.nuse++;
				return ts;
			}
		}


		public static TString luaS_newliteral (lua_State L, string str) {
			return luaS_newlstr(L, Encoding.UTF8.GetBytes (str));
		}


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
		public static int luaS_eqlngstr (TString a, TString b) {
			lua_assert (a.tt == LUA_TLNGSTR && b.tt == LUA_TLNGSTR);
			return ((a == b) ||  /* same instance or... */
			        ((a.len == b.len) &&  /* equal length and ... */
			 		lstring.memcmp (a.data, b.data)) ? 1 : 0);  /* equal contents */
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
		public static TString luaS_newlstr (lua_State L, byte[] str) {
			if (str.Length <= LUAI_MAXSHORTLEN)  /* short string? */
                return lstring.internshrstr (L, str);
			else {
				if ((str.Length + 1) > MAX_SIZE)
					luaM_toobig (L);
                return lstring.createstrobj (L, str, LUA_TLNGSTR, G (L).seed);
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
