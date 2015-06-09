using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lparser {







            public static void retstat (LexState ls) {
                /* stat -> RETURN [explist] [';'] */
                FuncState fs = ls.fs;
                expdesc e = new expdesc ();
                int first = 0;
                int nret = 0;  /* registers with returned values */
                if (block_follow (ls, 1) || ls.t.token == ';')
                    nret = 0;  /* return no values */
                else {
                    nret = explist (ls, e);  /* optional return values */
                    if (hasmultret (e.k)) {
                        luaK_setmultret (fs, e);
                        if (e.k == expkind.VCALL && nret == 1) {
                            SET_OPCODE (getcode (fs, e), OpCode.OP_TAILCALL);  /* tail call? */
                            lua_assert (GETARG_A (getcode (fs, e)) == fs.nactvar);
                        }
                        first = fs.nactvar;
                        nret = lua530.LUA_MULTRET;  /* return all values */
                    }
                    else {
                        if (nret == 1)  /* only one single value? */
                            first = luaK_exp2anyreg (fs, e);
                        else {
                            luaK_exp2nextreg (fs, e);  /* values must go to the stack */
                            first = fs.nactvar;  /* return all active values */
                            lua_assert (nret == fs.freereg - first);
                        }
                    }
                }
                luaK_ret (fs, first, nret);
                testnext (ls, ';');  /* skip optional semicolon */
            }


            public static void statement (LexState ls) {
                int line = ls.linenumber;  /* may be needed for error messages */
                enterlevel (ls);
                switch (ls.t.token) {
                    case ';': {  /* stat -> ';' (empty statement) */
                        luaX_next (ls);  /* skip ';' */
                        break;
                    }
                    case (int)RESERVED.TK_IF: {  /* stat -> ifstat */
                        ifstat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_WHILE: {  /* stat -> whilestat */
                        whilestat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_DO: {  /* stat -> DO block END */
                        luaX_next (ls);  /* skip DO */
                        block (ls);
                        check_match (ls, RESERVED.TK_END, RESERVED.TK_DO, line);
                        break;
                    }
                    case (int)RESERVED.TK_FOR: {  /* stat -> forstat */
                        forstat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_REPEAT: {  /* stat -> repeatstat */
                        repeatstat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_FUNCTION: {  /* stat -> funcstat */
                        funcstat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_LOCAL: {  /* stat -> localstat */
                        luaX_next (ls);  /* skip LOCAL */
                        if (testnext (ls, RESERVED.TK_FUNCTION))  /* local function? */
                            localfunc (ls);
                        else                              
                            localstat (ls, line);
                        break;
                    }
                    case (int)RESERVED.TK_DBCOLON: {  /* stat -> label */
                        luaX_next (ls);  /* skip double colon */
                        labelstat (ls, str_checkname (ls), line);
                        break;
                    }
                    case (int)RESERVED.TK_RETURN: {  /* stat -> retstat */
                        luaX_next (ls);  /* skip RETURN */
                        retstat (ls);
                        break;
                    }
                    case (int)RESERVED.TK_BREAK: goto case (int)RESERVED.TK_GOTO;  /* stat -> breakstat */
                    case (int)RESERVED.TK_GOTO: {  /* stat -> 'goto' NAME */
                        gotostat (ls, luaK_jump (ls.fs));
                        break;
                    }
                    default: {  /* stat -> func | assignment */
                        exprstat (ls);
                        break;
                    }
                }
                lua_assert (ls.fs.f.maxstacksize >= ls.fs.freereg &&
                                ls.fs.freereg >= ls.fs.nactvar);
                ls.fs.freereg = ls.fs.nactvar;  /* free registers */
                leavelevel (ls);
            }


			/*
			** compiles the main function, which is a regular vararg function with an
			** upvalue named LUA_ENV
			*/
			public static void mainfunc (LexState ls, FuncState fs) {
				BlockCnt bl = new BlockCnt ();
				expdesc v = new expdesc ();
				open_func (ls, fs, bl);
                fs.f.is_vararg = 1;  /* main function is always vararg */
                init_exp (v, expkind.VLOCAL, 0);  /* create and... */
                newupvalue (fs, ls.envn, v);  /* ...set environment upvalue */
                luaX_next (ls);  /* read first token */
                statlist (ls);  /* parse main body */
                check (ls, RESERVED.TK_EOS);
                close_func (ls);
			}
        }


        /*
        ** Expression descriptor
        */
        public enum expkind {
            VVOID,  /* no value */
            VNIL,
            VTRUE,
            VFALSE,
            VK,  /* info = index of constant in 'k' */
            VKFLT,  /* nval = numerical float value */
            VKINT,  /* nval = numerical integer value */
            VNONRELOC,  /* info = result register */
            VLOCAL,  /* info = local register */
            VUPVAL,  /* info = index of upvalue in 'upvalues' */
            VINDEXED,  /* t = table register/upvalue; idx = index R/K */
            VJMP,  /* info = instruction pc */
            VRELOCABLE,  /* info = instruction pc */
            VCALL,  /* info = instruction pc */
            VVARARG  /* info = instruction pc */
        };


        public static bool vkisvar (expkind k) { return (expkind.VLOCAL <= k && k <= expkind.VINDEXED); }
        public static bool vkisinreg (expkind k) { return (k == expkind.VNONRELOC || k == expkind.VLOCAL); }

        public class expdesc {
            public class cu {
                public class ci {
                    public short idx;  /* index (R/K) */
                    public byte t;  /* table (register or upvalue) */
                    public byte vt;  /* whether 't' is register (VLOCAL) or upvalue (VUPVAL) */
                }

                public ci ind;  /* for indexed variables (VINDEXED) */
                public int info;  /* for generic use */
                public double nval;  /* for VKFLT */
                public long ival;  /* for VKINT */

                public cu () {
                    ind = new ci ();
                }
            }

            public expkind k;
            public cu u;
            public int t;  /* patch list of 'exit when true' */
            public int f;  /* patch list of 'exit when false' */

            public expdesc () {
                u = new cu ();
            }
        }


        /* description of active local variable */
        public class Vardesc {
            public short idx;  /* variable index in stack */
        }


        /* description of pending goto statements and label statements */
        public class Labeldesc {
            public TString name;  /* label identifier */
            public int pc;  /* position in code */
            public int line;  /* line where it appeared */
            public byte nactvar;  /* local level where it appears in current block */
        }


        /* list of labels or gotos */
        public class Labellist {
            public Labeldesc arr;  /* array */
            public int n;  /* number of entries in use */
            public int size;  /* array size */
        }


        /* dynamic structures used by the parser */
        public class Dyndata {
            public class ca {
                public Vardesc arr;
                public int n;
                public int size;
            }

            public ca actvar;  /* list of active local variables */
            public Labellist gt;  /* list of pending gotos */
            public Labellist label;  /* list of active labels */

            public Dyndata () {
                actvar = new ca ();
                gt = new Labellist ();
                label = new Labellist ();
            }
        }


        /*
        ** nodes for block list (list of active blocks)
        */
        public class BlockCnt {
            public BlockCnt previous;  /* chain */
            public int firstlabel;  /* index of first label in this block */
            public int firstgoto;  /* index of first pending goto in this block */
            public byte nactvar;  /* # active locals outside the block */
            public byte upval;  /* true if some variable in the block is an upvalue */
            public byte isloop;  /* true if 'block' is a loop */
        }


        /* state needed to generate code for a given function */
        public class FuncState {
            public Proto f;  /* current function header */
            public FuncState prev;  /* enclosing function */
            public LexState ls;  /* lexical state */
            public BlockCnt bl;  /* chain of current blocks */
            public int pc;  /* next position to code (equivalent to 'ncode') */
            public int lasttarget;  /* 'label' of last 'jump label' */
            public int jpc;  /* list of pending jumps to 'pc' */
            public int nk;  /* number of elements in 'k' */
            public int np;  /* number of elements in 'p' */
            public int firstlocal;  /* index of first local var (in Dyndata array) */
            public short nlocvars;  /* number of elements in 'f->locvars' */
            public byte nactvar;  /* number of active local variables */
            public byte nups;  /* number of upvalues */
            public byte freereg;  /* first free register */
        }


        public static LClosure luaY_parser (lua_State L, Zio z, MBuffer buff, Dyndata dyd, string name, int firstchar) {
			LexState lexstate = new LexState ();
			FuncState funcstate = new FuncState ();
            LClosure cl = luaF_newLclosure (L, 1);  /* create main closure */
            setclLvalue (L, L.top, cl);  /* anchor it (to avoid being collected) */
            incr_top (L);
			lexstate.h = luaH_new (L);  /* create table for scanner */
			sethvalue (L, L.top, lexstate.h);  /* anchor it */
			incr_top (L);
			funcstate.f = luaF_newproto (L);
			cl.p = funcstate.f;
			funcstate.f.source = luaS_new (L, name);  /* create and anchor TString */
			lua_assert (iswhite (funcstate.f));  /* do not need barrier here */
			lexstate.buff = buff;
			lexstate.dyd = dyd;
			dyd.actvar.n = 0;
			dyd.gt.n = 0;
			dyd.label.n = 0;
			luaX_setinput (L, lexstate, z, funcstate.f.source, firstchar);
			lparser.mainfunc (lexstate, funcstate);
			lua_assert (funcstate.prev == null && funcstate.nups == 1 && lexstate.fs == null);
			/* all scopes should be correctly finished */
			lua_assert (dyd.actvar.n == 0 && dyd.gt.n == 0 && dyd.label.n == 0);
			L.top--;  /* remove scanner's table */
			return cl;  /* closure is on the stack, too */
        }

















    }
}