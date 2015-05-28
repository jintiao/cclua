using System;

namespace cclua53
{
    public static partial class imp {

        public static T luaM_newobject<T> () where T : new () {
            return new T ();
        }

        public static T[] luaM_newvector<T> (cclua.lua_State L, int n) where T : new () {
            T[] block = new T[n];
            for (int i = 0; i < n; i++)
                block[i] = new T ();
            return block;
        }
    }
}
