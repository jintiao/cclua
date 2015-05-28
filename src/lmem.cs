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

        public static T[] luaM_newvector<T> (cclua.lua_State L, int n) where T : new () {
            T[] block = new T[n];
            for (int i = 0; i < n; i++)
                block[i] = new T ();
            return block;
        }

		public static T[] luaM_reallocv<T> (cclua.lua_State L, T[] block, uint osize, uint nsize) where T : new () {
			if (nsize == 0)
				return null;

			uint realosize = (block != null) ? osize : 0;
			lua_assert ((realosize == 0) == (block == null));
			T[] newblock = luaM_newvector<T> (L, nsize);
			if (newblock == null && nsize > 0) {
				api_check (nsize > realosize, "realloc cannot fail when shrinking a block");
				luaD_throw (L, cclua.LUA_ERRMEM);
			}
			lua_assert ((nsize == 0) == (newblock == null));	
			if (nsize < osize) {
				for (uint i = 0; i < nsize; i++) {
					// TODO : object value copy
				}
			}
			else {
				for (uint i = 0; i < osize; i++) {
					// TODO : object value copy
				}
				for (uint i = osize; i < nsize; i++) {
					// TODO : set nil value
				}
			}
			return newblock;
		}

		public static void luaM_reallocvector<T> (cclua.lua_State L, ref T[] block, uint osize, uint nsize) where T : new () {
			block = luaM_reallocv<T> (L, block, osize, nsize);
		}
    }
}
