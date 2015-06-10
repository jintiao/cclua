﻿using System;

namespace cclua {

    public static partial class lua530 {

		public static long LUA_VERSION_NUM = 503;


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
            public sbyte isvararg;  /* (u) */
            public sbyte istailcall;  /* (t) */
            public sbyte[] short_src;  /* (S) */
            /* private part */
            public imp.CallInfo i_ci;  /* active function */
        };

        public static string lua_tostring (lua_State L, int idx) {
            ulong m = 0;
            return lua_tolstring (L, idx, ref m);
        }
    }
}
