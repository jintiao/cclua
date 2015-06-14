#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBPLAYER || UNITY_WII || UNITY_IOS || UNITY_IPHONE || UNITY_ANDROID || UNITY_PS3 || UNITY_PS4 || UNITY_XBOX360 || UNITY_XBOXONE || UNITY_BLACKBERRY || UNITY_TIZEN || UNITY_WINRT || UNITY_WEBGL
#else

using System;
using System.IO;

namespace cclua {

    public static partial class lua530 {

        /*
        ** {==================================================================
        ** "Abstraction Layer" for basic report of messages and errors
        ** ===================================================================
        */


        /* print a string */
        public static void lua_writestring (string s) { /* TODO */
            Console.Write (s);
        }

        /* print a newline and flush the output */
        public static void lua_writeline () { /* TODO */
            Console.WriteLine ();
        }

        /* print an error message */
        public static void lua_writestringerror (string fmt, params object[] args) {  /* TODO */
            Console.Write (fmt, args);
        }

    }
}

#endif
