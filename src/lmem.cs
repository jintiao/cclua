using System;

namespace cclua53
{
    public static partial class imp {

		public static void luaM_toobig (cclua.lua_State L) {
			luaG_runerror (L, "memory allocation error: block too big");
		}

        public static T luaM_newobject<T> () where T : new () {
            return new T ();
        }

        public static T[] luaM_emptyvector<T> (cclua.lua_State L, long n) {
            if (n <= 0)
                return null;
            return new T[n];
        }

        public static T[] luaM_fullvector<T> (cclua.lua_State L, long n) where T : new () {
            if (n <= 0)
                return null;
            T[] block = new T[n];
            for (long i = 0; i < n; i++)
                block[i] = new T ();
            return block;
        }

        public static T[] luaM_reallocv<T> (cclua.lua_State L, T[] block, long osize, long nsize) where T : new () {
			if (osize < 0 || nsize <= 0)  /* nsize==0 means free */
				return null;

            long realosize = (block != null) ? osize : 0;
			lua_assert ((realosize == 0) == (block == null));
            T[] newblock = luaM_emptyvector<T> (L, nsize);
			if (newblock == null && nsize > 0) {
				api_check (nsize > realosize, "realloc cannot fail when shrinking a block");
				luaD_throw (L, cclua.LUA_ERRMEM);
			}
			lua_assert ((nsize == 0) == (newblock == null));
            long minsize = Math.Min (osize, nsize);
            for (long i = 0; i < minsize; i++) {
                newblock[i] = block[i];
            }
			return newblock;
		}

        public static void luaM_reallocvector<T> (cclua.lua_State L, ref T[] block, long osize, long nsize) where T : new () {
			block = luaM_reallocv<T> (L, block, osize, nsize);
		}

        public static void luaM_freearray (cclua.lua_State L, object block) {
            /* do nothing */
        }

        public static void luaM_free (cclua.lua_State L, object block) {
            /* do nothing */
        }
    }
}
