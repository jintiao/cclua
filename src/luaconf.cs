using System;

namespace cclua53
{
    public static partial class imp {

        /*
        @@ LUA_EXTRASPACE defines the size of a raw memory area associated with
        ** a Lua state with very fast access.
        ** CHANGE it if you need a different size.
        */
        public const int LUA_EXTRASPACE = sizeof (long);

		/*
		@@ LUAI_MAXSHORTLEN is the maximum length for short strings, that is,
		** strings that are internalized. (Cannot be smaller than reserved words
		** or tags for metamethods, as these strings must be internalized;
		** #("function") = 8, #("__newindex") = 10.)
		*/
		public const int LUAI_MAXSHORTLEN = 40;
    }
}
