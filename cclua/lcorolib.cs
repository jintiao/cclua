using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using lua_Debug = cclua.lua530.lua_Debug;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lco {

            public static lua_State getco (lua_State L) {
                lua_State co = cc.lua_tothread (L, 1);
                cc.luaL_argcheck (L, co != null, 1, "thread expected");
                return co;
            }


            public static int auxresume (lua_State L, lua_State co, int narg) {
                if (cc.lua_checkstack (co, narg) == 0) {
                    cc.lua_pushliteral (L, "too many arguments to resume");
                    return -1;  /* error flag */
                }
                if (cc.lua_status (co) == cc.LUA_OK && cc.lua_gettop (co) == 0) {
                    cc.lua_pushliteral (L, "cannot resume dead coroutine");
                    return -1;  /* error flag */
                }
                cc.lua_xmove (L, co, narg);
                int status = cc.lua_resume (co, L, narg);
                if (status == cc.LUA_OK || status == cc.LUA_YIELD) {
                    int nres = cc.lua_gettop (co);
                    if (cc.lua_checkstack (L, nres + 1) == 0) {
                        cc.lua_pop (co, nres);  /* remove results anyway */
                        cc.lua_pushliteral (L, "too many results to resume");
                        return -1;  /* error flag */
                    }
                    cc.lua_xmove (co, L, nres);  /* move yielded values */
                    return nres;
                }
                else {
                    cc.lua_xmove (co, L, 1);
                    return -1;  /* error flag */
                }
            }
		}


        public static int luaB_coresume (lua_State L) {
            lua_State co = lco.getco (L);
            int r = lco.auxresume (L, co, cc.lua_gettop (L) - 1);
            if (r < 0) {
                cc.lua_pushboolean (L, 0);
                cc.lua_insert (L, -2);
                return 2;  /* return false + error message */
            }
            else {
                cc.lua_pushboolean (L, 1);
                cc.lua_insert (L, -(r + 1));
                return (r + 1);  /* return true + 'resume' returns */
            }
        }


        public static int luaB_auxwrap (lua_State L) {
            lua_State co = cc.lua_tothread (L, cc.lua_upvalueindex (1));
            int r = lco.auxresume (L, co, cc.lua_gettop (L));
            if (r < 0) {
                if (cc.lua_isstring (L, -1) != 0) {  /* error object is a string? */
                    cc.luaL_where (L, 1);  /* add extra info */
                    cc.lua_insert (L, -2);
                    cc.lua_concat (L, 2);
                }
                return cc.lua_error (L);  /* propagate error */
            }
            return r;
        }


        public static int luaB_cocreate (lua_State L) {
            lua_State NL;
            cc.luaL_checktype (L, 1, cc.LUA_TFUNCTION);
            NL = cc.lua_newthread (L);
            cc.lua_pushvalue (L, 1);  /* move function to top */
            cc.lua_xmove (L, NL, 1);  /* move function from L to NL */
            return 1;
        }


        public static int luaB_cowrap (lua_State L) {
            luaB_cocreate (L);
            cc.lua_pushcclosure (L, luaB_auxwrap, 1);
            return 1;
        }


        public static int luaB_yield (lua_State L) {
            return cc.lua_yield (L, cc.lua_gettop (L));
        }


        public static int luaB_costatus (lua_State L) {
            lua_State co = lco.getco (L);
            if (L == co) cc.lua_pushliteral (L, "running");
            else {
                switch (cc.lua_status (co)) {
                case cc.LUA_YIELD:
                    cc.lua_pushliteral (L, "suspended");
                    break;
                case cc.LUA_OK: {
                    lua_Debug ar = new lua_Debug ();
                    if (cc.lua_getstack (co, 0, ar) > 0)
                        cc.lua_pushliteral (L, "normal");
                    else if (cc.lua_gettop (co) == 0)
                        cc.lua_pushliteral (L, "dead");
                    else
                        cc.lua_pushliteral (L, "suspended");
                    break;
                }
                default:
                    cc.lua_pushliteral (L, "dead");
                    break;

                }
            }
            return 1;
        }


        public static int luaB_yieldable (lua_State L) {
            cc.lua_pushboolean (L, cc.lua_isyieldable (L));
            return 1;
        }


        public static int luaB_corunning (lua_State L) {
            int ismain = cc.lua_pushthread (L);
            cc.lua_pushboolean (L, ismain);
            return 2;
        }


        public static luaL_Reg[] co_funcs = {
			new luaL_Reg ("create", luaB_cocreate),
			new luaL_Reg ("resume", luaB_coresume),
			new luaL_Reg ("running", luaB_corunning),
			new luaL_Reg ("status", luaB_costatus),
			new luaL_Reg ("wrap", luaB_cowrap),
			new luaL_Reg ("yield", luaB_yield),
			new luaL_Reg ("isyieldable", luaB_yieldable),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {
		
		public static int luaopen_coroutine (lua_State L) {
            cc.luaL_newlib (L, imp.co_funcs);
			return 1;
		}
	}
}