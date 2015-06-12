using System;

namespace cclua {

    public static partial class lua530 {

		public static string LUA_VERSION_MAJOR = "5";
		public static string LUA_VERSION_MINOR = "3";
        public static double LUA_VERSION_NUM = 503;
		public static string LUA_VERSION_RELEASE = "0";

		public static string LUA_VERSION = "Lua " + LUA_VERSION_MAJOR + "." + LUA_VERSION_MINOR;
        public static string LUA_RELEASE = LUA_VERSION + "." + LUA_VERSION_RELEASE;
        public static string LUA_COPYRIGHT = LUA_RELEASE + "  Copyright (C) 1994-2015 Lua.org, PUC-Rio. Port by jintiao";
        public static string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo, W. Celes, jintiao";


        /* mark for precompiled code ('<esc>Lua') */
        public static byte[] LUA_SIGNATURE = imp.str2byte ("\x1bLua");


        /* option for multiple returns in 'lua_pcall' and 'lua_call' */
        public const int LUA_MULTRET = -1;


        /*
        ** pseudo-indices
        */
        public const int LUA_REGISTRYINDEX = imp.LUAI_FIRSTPSEUDOIDX;
        public static int lua_upvalueindex (int i) { return (LUA_REGISTRYINDEX - i); }


        /* thread status */
        public const int LUA_OK = 0;
        public const int LUA_YIELD = 1;
        public const int LUA_ERRRUN = 2;
        public const int LUA_ERRSYNTAX = 3;
        public const int LUA_ERRMEM = 4;
        public const int LUA_ERRGCMM = 5;
        public const int LUA_ERRERR = 6;

        /*
        ** basic types
        */
        public const int LUA_TNONE = -1;
        public const int LUA_TNIL = 0;
        public const int LUA_TBOOLEAN = 1;
        public const int LUA_TLIGHTUSERDATA = 2;
        public const int LUA_TNUMBER = 3;
        public const int LUA_TSTRING = 4;
        public const int LUA_TTABLE = 5;
        public const int LUA_TFUNCTION = 6;
        public const int LUA_TUSERDATA = 7;
        public const int LUA_TTHREAD = 8;
        public const int LUA_NUMTAGS = 9;




        /* minimum Lua stack available to a C function */
        public const int LUA_MINSTACK = 20;



        /* predefined values in the registry */
        public const int LUA_RIDX_MAINTHREAD = 1;
        public const int LUA_RIDX_GLOBALS = 2;
        public const int LUA_RIDX_LAST = LUA_RIDX_GLOBALS;

        /*
        ** Type for C functions registered with Lua
        */
        public delegate int lua_CFunction (lua_State L);

        /*
        ** Type for continuation functions
        */
        public delegate int lua_KFunction (lua_State L, int status, long ctx);


        /*
        ** Type for functions that read/write blocks when loading/dumping Lua chunks
        */
        public delegate byte[] lua_Reader (lua_State L, object ud, ref int sz);

        public delegate int lua_Writer (lua_State L, byte[] p, int sz, object ud);


        /*
        ** Type for memory-allocation functions
        */
        public interface lua_Alloc {
        }


        /* Functions to be called by the debugger in specific events */
        public delegate void lua_Hook (lua_State L, lua_Debug ar);



        /*
        ** Comparison and arithmetic functions
        */
        public const int LUA_OPADD = 0;  /* ORDER TM, ORDER OP */
        public const int LUA_OPSUB = 1;
        public const int LUA_OPMUL = 2;
        public const int LUA_OPMOD = 3;
        public const int LUA_OPPOW = 4;
        public const int LUA_OPDIV = 5;
        public const int LUA_OPIDIV = 6;
        public const int LUA_OPBAND = 7;
        public const int LUA_OPBOR = 8;
        public const int LUA_OPBXOR = 9;
        public const int LUA_OPSHL = 10;
        public const int LUA_OPSHR = 11;
        public const int LUA_OPUNM = 12;
        public const int LUA_OPBNOT = 13;


        public const int LUA_OPEQ = 0;
        public const int LUA_OPLT = 1;
        public const int LUA_OPLE = 2;



		/*
		** 'load' and 'call' functions (load and run Lua code)
		*/
		
		public static void lua_call (lua_State L, int n, int r) { lua_callk (L, n, r, 0, null); }

		public static int lua_pcall (lua_State L, int n, int r, int f) { return lua_pcallk (L, n, r, f, 0, null); }



		/*
		** coroutine functions
		*/

		public static int lua_yield (lua_State L, int r) { return lua_yieldk (L, r, 0, null); }



        /*
        ** garbage-collection function and options
        */
        public const int LUA_GCSTOP = 0;
        public const int LUA_GCRESTART = 1;
        public const int LUA_GCCOLLECT = 2;
        public const int LUA_GCCOUNT = 3;
        public const int LUA_GCCOUNTB = 4;
        public const int LUA_GCSTEP = 5;
        public const int LUA_GCSETPAUSE = 6;
        public const int LUA_GCSETSTEPMUL = 7;
        public const int LUA_GCISRUNNING = 9;





		/*
		** {==============================================================
		** some useful macros
		** ===============================================================
		*/

		public static byte[] lua_getextraspace (lua_State L) { return L.lg.l.extra_; }

		public static double lua_tonumber (lua_State L, int idx) { int i = 0; return lua_tonumberx (L, idx, ref i); }
		public static long lua_tointeger (lua_State L, int idx) { int i = 0; return lua_tointegerx (L, idx, ref i); }

		public static void lua_pop (lua_State L, int n) { lua_settop (L, -n - 1); }

		public static void lua_newtable (lua_State L) { lua_createtable (L, 0, 0); }

		public static void lua_register (lua_State L, string n, lua_CFunction f) { lua_pushcfunction (L, f); lua_setglobal (L, n); }

		public static void lua_pushcfunction (lua_State L, lua_CFunction f) { lua_pushcclosure (L, f, 0); }

		public static int lua_isfunction (lua_State L, int n) { return (lua_type (L, n) == LUA_TFUNCTION ? 1 : 0); }
		public static int lua_istable (lua_State L, int n) { return (lua_type (L, n) == LUA_TTABLE ? 1 : 0); }
		public static int lua_islightuserdata (lua_State L, int n) { return (lua_type (L, n) == LUA_TLIGHTUSERDATA ? 1 : 0); }
		public static int lua_isnil (lua_State L, int n) { return (lua_type (L, n) == LUA_TNIL ? 1 : 0); }
		public static int lua_isboolean (lua_State L, int n) { return (lua_type (L, n) == LUA_TBOOLEAN ? 1 : 0); }
		public static int lua_isthread (lua_State L, int n) { return (lua_type (L, n) == LUA_TTHREAD ? 1 : 0); }
		public static int lua_isnone (lua_State L, int n) { return (lua_type (L, n) == LUA_TNONE ? 1 : 0); }
		public static int lua_isnoneornil (lua_State L, int n) { return (lua_type (L, n) <= 0 ? 1 : 0); }

		public static void lua_pushliteral (lua_State L, string s) { lua_pushlstring (L, s); }

		public static void lua_pushglobaltable (lua_State L) { lua_rawgeti (L, LUA_REGISTRYINDEX, LUA_RIDX_GLOBALS); }

        public static string lua_tostring (lua_State L, int i) { int n = 0; return lua_tolstring (L, i, ref n); }


		public static void lua_insert (lua_State L, int idx) { lua_rotate (L, idx, 1); }

		public static void lua_remove (lua_State L, int idx) { lua_rotate (L, idx, -1); lua_pop (L, 1); }

		public static void lua_replace (lua_State L, int idx) { lua_copy (L, -1, idx); lua_pop (L, 1); }








        /*
        ** {======================================================================
        ** Debug API
        ** =======================================================================
        */


        /*
        ** Event codes
        */
        public const int LUA_HOOKCALL = 0;
        public const int LUA_HOOKRET = 1;
        public const int LUA_HOOKLINE = 2;
        public const int LUA_HOOKCOUNT = 3;
        public const int LUA_HOOKTAILCALL = 4;


        /*
        ** Event masks
        */
        public const int LUA_MASKCALL = 1 << LUA_HOOKCALL;
        public const int LUA_MASKRET = 1 << LUA_HOOKRET;
        public const int LUA_MASKLINE = 1 << LUA_HOOKLINE;
        public const int LUA_MASKCOUNT = 1 << LUA_HOOKCOUNT;



        public class lua_Debug {
            public int ev;
            public string name;  /* (n) */
            public string namewhat;  /* (n) 'global', 'local', 'field', 'method' */
            public string what;  /* (S) 'Lua', 'C', 'main', 'tail' */
            public string source;  /* (S) */
            public int currentline;  /* (l) */
            public int linedefined;  /* (S) */
            public int lastlinedefined;  /* (S) */
            public byte nups;  /* (u) number of upvalues */
            public byte nparams;  /* (u) number of parameters */
            public byte isvararg;  /* (u) */
            public byte istailcall;  /* (t) */
            public string short_src;  /* (S) */
            /* private part */
            public imp.CallInfo i_ci;  /* active function */
        };
    }
}
