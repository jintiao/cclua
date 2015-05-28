using System;

namespace cclua53
{
    public static partial class imp {

        public static T luaC_newobj<T> (cclua.lua_State L, int tt) where T : GCObject, new () {
            T o = luaM_newobject<T> ();
            o.tt = (byte)tt;
            return o;
        }
    }
}
