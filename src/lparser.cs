using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lparser {

			/* maximum number of local variables per function (must be smaller
   				than 250, due to the bytecode format) */
			public const int MAXVARS = 200;


			public static bool hasmultret (expkind k) { return (k == expkind.VCALL || k == expkind.VVARARG); }


			/* because all strings are unified by the scanner, the parser
   				can use pointer equality for string equality */
			public static bool eqstr (TString a, TString b) { return (a == b); }


			/* semantic error */
			public static void semerror (LexState ls, string msg) {
				ls.t.token = 0;  /* remove "near <token>" from final message */
				luaX_syntaxerror (ls, msg);
			}


			public static void error_expected (LexState ls, int token) {
				luaX_syntaxerror (ls, luaO_pushfstring (ls.L, "%s expected", luaX_token2str (ls, token)));
			}


			public static void errorlimit (FuncState fs, int limit, string what) {
				lua_State L = fs.ls.L;
				int line = fs.f.linedefined;
				string where = (line == 0) ? "main function" : luaO_pushfstring (L, "function at line %d", line);
				string msg = luaO_pushfstring (L, "too many %s (limit is %d) in %s", what, limit, where);
				luaX_syntaxerror (fs.ls, msg);
			}


			public static void checklimit (FuncState fs, int v, int l, string what) {
				if (v > l) errorlimit (fs, l, what);
			}


			public static bool testnext (LexState ls, int c) {
				if (ls.t.token == c) {
					luaX_next (ls);
					return true;
				}
				else return false;
			}


			public static void check (LexState ls, int c) {
				if (ls.t.token != c)
					error_expected (ls, c);
			}


			public static void checknext (LexState ls, int c) {
				check (ls, c);
				luaX_next (ls);
			}


			public static void check_condition (LexState ls, bool c, string msg) { if (c == false) luaX_syntaxerror (ls, msg); }


			public static void check_match (LexState ls, int what, int who, int where) {
				if (testnext (ls, what) == false) {
					if (where == ls.linenumber)
						error_expected (ls, what);
					else {
						luaX_syntaxerror (ls, luaO_pushfstring (ls.L, "%s expected (to close %s at line %d)", luaX_token2str (ls, what), luaX_token2str (ls, who), where));
					}
				}
			}


			public static TString str_checkname (LexState ls) {
				check (ls, (int)RESERVED.TK_NAME);
				TString ts = (TString)ls.t.seminfo.o;
				luaX_next (ls);
				return ts;
			}


			public static void init_exp (expdesc e, expkind k, int i) {
				e.f = NO_JUMP;
				e.t = NO_JUMP;
				e.k = k;
				e.u.info = i;
			}


			public static void codestring (LexState ls, expdesc e, TString s) {
				init_exp (e, expkind.VK, luaK_stringK (ls.fs, s));
			}


			public static void checkname (LexState ls, expdesc e) {
				codestring (ls, e, str_checkname (ls));
			}


			public static int registerlocalvar (LexState ls, TString varname) {
				FuncState fs = ls.fs;
				Proto f = fs.f;
				int oldsize = f.sizelocvars;
				luaM_growvector<LocVar> (ls.L, ref f.locvars, fs.nlocvars, f.sizelocvars, SHRT_MAX, "local variables");
				while (oldsize < f.sizelocvars) f.locvars[oldsize++].varname = null;
				f.locvars[fs.nlocvars].varname = varname;
				luaC_objbarrier (ls.L, f, varname);
				return fs.nlocvars++;
			}


			public static void new_localvar (LexState ls, TString name) {
				FuncState fs = ls.fs;
				Dyndata dyd = ls.dyd;
				int reg = registerlocalvar (ls, name);
				checklimit (fs, dyd.actvar.n + 1 - fs.firstlocal, MAXVARS, "local variables");
                luaM_growvector<Vardesc> (ls.L, ref dyd.actvar.arr, dyd.actvar.n + 1, dyd.actvar.size, MAX_INT, "local variables");
				dyd.actvar.arr[dyd.actvar.n++].idx = (short)reg;
			}


			public static void new_localvarliteral_ (LexState ls, byte[] name, int sz) {
                new_localvar (ls, luaX_newstring (ls, name, sz));
			}


			public static void new_localvalliteral (LexState ls, string v) {
				byte[] str = str2byte (v);
				new_localvarliteral_ (ls, str, str.Length);
			}


			public static LocVar getlocvar (FuncState fs, int i) {
				int idx = fs.ls.dyd.actvar.arr[fs.firstlocal + i].idx;
				lua_assert (idx < fs.nlocvars);
				return fs.f.locvars[idx];
			}


			public static void adjustlocalvars (LexState ls, int nvars) {
				FuncState fs = ls.fs;
				fs.nactvar = (byte)(fs.nactvar + nvars);
				for (; nvars != 0; nvars--)
					getlocvar (fs, fs.nactvar - nvars).startpc = fs.pc;
			}


			public static void removevars (FuncState fs, int tolevel) {
				fs.ls.dyd.actvar.n -= fs.nactvar - tolevel;
				while (fs.nactvar > tolevel)
					getlocvar (fs, --fs.nactvar).endpc = fs.pc;
			}


			public static int searchupvalue (FuncState fs, TString name) {
				Upvaldesc[] up = fs.f.upvalues;
				for (int i = 0; i < fs.nups; i++)
					if (eqstr (up[i].name, name)) return i;
				return -1;  /* not found */
			}


			public static int newupvalue (FuncState fs, TString name, expdesc v) {
				Proto f = fs.f;
				int oldsize = f.sizeupvalues;
				checklimit (fs, fs.nups + 1, MAXUPVAL, "upvalues");
				luaM_growvector<Upvaldesc> (fs.ls.L, ref f.upvalues, fs.nups, f.sizeupvalues, MAXUPVAL, "upvalues");
				while (oldsize < f.sizeupvalues) f.upvalues[oldsize++].name = null;
				f.upvalues[fs.nups].instack = (byte)((v.k == expkind.VLOCAL) ? 1 : 0);
				f.upvalues[fs.nups].idx = (byte)v.u.info;
				f.upvalues[fs.nups].name = name;
				luaC_objbarrier (fs.ls.L, f, name);
				return fs.nups++;
			}


			public static int searchvar (FuncState fs, TString n) {
				for (int i = fs.nactvar - 1; i >= 0; i--)
					if (eqstr (n, getlocvar (fs, i).varname)) return i;
				return -1;  /* not found */
			}


			/*
			  Mark block where variable at given level was defined
			  (to emit close instructions later).
			*/
			public static void markupval (FuncState fs, int level) {
				BlockCnt bl = fs.bl;
				while (bl.nactvar > level) bl = bl.previous;
				bl.upval = 1;
			}


			/*
			  Find variable with given name 'n'. If it is an upvalue, add this
			  upvalue into all intermediate functions.
			*/
			public static expkind singlevaraux (FuncState fs, TString n, expdesc var, bool nbase) {
				if (fs == null)  /* no more levels? */
					return expkind.VVOID;  /* default is global */
				else {
					int v = searchvar (fs, n);  /* look up locals at current level */
					if (v >= 0) {  /* found? */
						init_exp (var, expkind.VLOCAL, v);  /* variable is local */
						if (nbase == false)
							markupval (fs, v);  /* local will be used as an upval */
						return expkind.VLOCAL;
					}
					else {  /* not found as local at current level; try upvalues */
						int idx = searchupvalue (fs, n);  /* try existing upvalues */
						if (idx < 0) {  /* not found? */
							if (singlevaraux (fs.prev, n, var, false) == expkind.VVOID)  /* try upper levels */
								return expkind.VVOID;  /* not found; is a global */
							/* else was LOCAL or UPVAL */
							idx = newupvalue (fs, n, var);  /* will be a new upvalue */
						}
						init_exp (var, expkind.VUPVAL, idx);
						return expkind.VUPVAL;
					}
				}
			}


			public static void singlevar (LexState ls, expdesc var) {
				TString varname = str_checkname (ls);
				FuncState fs = ls.fs;
				if (singlevaraux (fs, varname, var, true) == expkind.VVOID) {  /* global name? */
					expdesc key = new expdesc ();
					singlevaraux (fs, ls.envn, var, true);  /* get environment variable */
					lua_assert (var.k == expkind.VLOCAL || var.k == expkind.VUPVAL);
					codestring (ls, key, varname);  /* key is variable name */
					luaK_indexed (fs, var, key);  /* env[varname] */
				}
			}


			public static void adjust_assign (LexState lst, int nvars, int nexps, expdesc e) {
				FuncState fs = lst.fs;
				int extra = nvars - nexps;
				if (hasmultret (e.k)) {
					extra++;
					if (extra < 0) extra = 0;
					luaK_setreturns (fs, e, extra);
					if (extra > 1) luaK_reserveregs (fs, extra - 1);
				}
				else {
					if (e.k != expkind.VVOID) luaK_exp2nextreg (fs, e);
					if (extra > 0) {
						int reg = fs.freereg;
						luaK_reserveregs (fs, extra);
						luaK_nil (fs, reg, extra);
					}
				}
			}


			public static void enterlevel (LexState ls) {
				lua_State L = ls.L;
				L.nCcalls++;
				checklimit (ls.fs, L.nCcalls, LUAI_MAXCCALLS, "C levels");
			}


			public static void leavelevel (LexState ls) { ls.L.nCcalls--; }


			public static void closegoto (LexState ls, int g, Labeldesc label) {
				FuncState fs = ls.fs;
				Labellist gl = ls.dyd.gt;
				Labeldesc gt = gl.arr[g];
				lua_assert (eqstr (gt.name, label.name));
				if (gt.nactvar < label.nactvar) {
					TString vname = getlocvar (fs, gt.nactvar).varname;
					string msg = luaO_pushfstring (ls.L,
					    "<goto %s> at line %d jumps into the scope of local '%s'",
					                               gt.name.data, gt.line, vname.data);
					semerror (ls, msg);
				}
				luaK_patchlist (fs, gt.pc, label.pc);
				for (int i = g; i < gl.n - 1; i++)
					gl.arr[i].copy (gl.arr[i + 1]);
				gl.n--;
			}


			/*
			** try to close a goto with existing labels; this solves backward jumps
			*/
			public static bool findlabel (LexState ls, int g) {
				BlockCnt bl = ls.fs.bl;
				Dyndata dyd = ls.dyd;
				Labeldesc gt = dyd.gt.arr[g];
				/* check labels in current block for a match */
				for (int i = bl.firstlabel; i < dyd.label.n; i++) {
					Labeldesc lb = dyd.label.arr[i];
					if (eqstr (lb.name, gt.name)) {  /* correct label? */
						if (gt.nactvar > lb.nactvar && (bl.upval != 0 || dyd.label.n > bl.firstlabel))
							luaK_patchclose (ls.fs, gt.pc, lb.nactvar);
						closegoto (ls, g, lb);  /* close it */
						return true;
					}
				}
				return false;
			}


			public static int newlabelentry (LexState ls, Labellist l, TString name, int line, int pc) {
				int n = l.n;
				luaM_growvector<Labeldesc> (ls.L, ref l.arr, n, l.size, SHRT_MAX, "labels/gotos");
				l.arr[n].name = name;
				l.arr[n].line = line;
				l.arr[n].nactvar = ls.fs.nactvar;
				l.arr[n].pc = pc;
				l.n = n + 1;
				return n;
			}


			/*
			** check whether new label 'lb' matches any pending gotos in current
			** block; solves forward jumps
			*/
			public static void findgotos (LexState ls, Labeldesc lb) {
				Labellist gl = ls.dyd.gt;
				int i = ls.fs.bl.firstgoto;
				while (i < gl.n) {
					if (eqstr (gl.arr[i].name, lb.name))
						closegoto (ls, i, lb);
					else
						i++;
				}
			}


			/*
			** export pending gotos to outer level, to check them against
			** outer labels; if the block being exited has upvalues, and
			** the goto exits the scope of any variable (which can be the
			** upvalue), close those variables being exited.
			*/
			public static void movegotosout (FuncState fs, BlockCnt bl) {
				int i = bl.firstgoto;
				Labellist gl = fs.ls.dyd.gt;
				/* correct pending gotos to current block and try to close it
 				    with visible labels */
				while (i < gl.n) {
					Labeldesc gt = gl.arr[i];
					if (gt.nactvar > bl.nactvar) {
						if (bl.upval != 0)
							luaK_patchclose (fs, gt.pc, bl.nactvar);
						gt.nactvar = bl.nactvar;
					}
					if (findlabel (fs.ls, i) == false)
						i++;  /* move to next one */
				}
			}


			public static void enterblock (FuncState fs, BlockCnt bl, byte isloop) {
				bl.isloop = isloop;
				bl.nactvar = fs.nactvar;
				bl.firstlabel = fs.ls.dyd.label.n;
				bl.firstgoto = fs.ls.dyd.gt.n;
				bl.upval = 0;
				bl.previous = fs.bl;
				fs.bl = bl;
				lua_assert (fs.freereg == fs.nactvar);
			}


			/*
			** create a label named 'break' to resolve break statements
			*/
			public static void breaklabel (LexState ls) {
				TString n = luaS_new (ls.L, "break");
				int l = newlabelentry (ls, ls.dyd.label, n, 0, ls.fs.pc);
				findgotos (ls, ls.dyd.label.arr[l]);
			}


			/*
			** generates an error for an undefined 'goto'; choose appropriate
			** message when label name is a reserved word (which can only be 'break')
			*/
			public static void undefgoto (LexState ls, Labeldesc gt) {
				string msg = isreserved (gt.name) 
								? "<%s> at line %d not inside a loop"
						: "no visible label '%s' for <goto> at line %d";
				msg = luaO_pushfstring (ls.L, msg, gt.name.data, gt.line);
				semerror (ls, msg);
			}


			public static void leaveblock (FuncState fs) {
				BlockCnt bl = fs.bl;
				LexState ls = fs.ls;
				if (bl.previous != null && bl.upval != 0) {
					/* create a 'jump to here' to close upvalues */
					int j = luaK_jump (fs);
					luaK_patchclose (fs, j, bl.nactvar);
					luaK_patchtohere (fs, j);
				}
				if (bl.isloop != 0)
					breaklabel (ls);  /* close pending breaks */
				fs.bl = bl.previous;
				removevars (fs, bl.nactvar);
				lua_assert (bl.nactvar == fs.nactvar);
				fs.freereg = fs.nactvar;  /* free registers */
				ls.dyd.label.n = bl.firstlabel;  /* remove local labels */
				if (bl.previous != null)  /* inner block? */
					movegotosout (fs, bl);  /* update pending gotos to outer block */
				else if (bl.firstgoto < ls.dyd.gt.n)  /* pending gotos in outer block? */
					undefgoto (ls, ls.dyd.gt.arr[bl.firstgoto]);  /* error */
			}


			/*
			** adds a new prototype into list of prototypes
			*/
			public static Proto addprototype (LexState ls) {
				lua_State L = ls.L;
				FuncState fs = ls.fs;
				Proto f = fs.f;
				if (fs.np >= f.sizep) {
					int oldsize = f.sizep;
					luaM_growvector<Proto> (L, ref f.p, fs.np, f.sizep, MAXARG_Bx, "functions");
					while (oldsize < f.sizep) f.p[oldsize++] = null;
				}
				Proto clp = luaF_newproto (L);
				f.p[fs.np++] = clp;
				luaC_objbarrier (L, f, clp);
				return clp;
			}


			/*
			** codes instruction to create new closure in parent function.
			** The OP_CLOSURE instruction must use the last available register,
			** so that, if it invokes the GC, the GC knows which registers
			** are in use at that time.
			*/
			public static void codeclosure (LexState ls, expdesc v) {
				FuncState fs = ls.fs.prev;
				init_exp (v, expkind.VRELOCABLE, luaK_codeABx (fs, OpCode.OP_CLOSURE, 0, fs.np - 1));
				luaK_exp2nextreg (fs, v);  /* fix it at the last register */
			}


			public static void open_func (LexState ls, FuncState fs, BlockCnt bl) {
				fs.prev = ls.fs;  /* linked list of funcstates */
				fs.ls = ls;
				ls.fs = fs;
				fs.pc = 0;
				fs.lasttarget = 0;
				fs.jpc = NO_JUMP;
				fs.freereg = 0;
				fs.nk = 0;
				fs.np = 0;
				fs.nups = 0;
				fs.nlocvars = 0;
				fs.nactvar = 0;
				fs.firstlocal = ls.dyd.actvar.n;
				fs.bl = null;
				Proto f = fs.f;
				f.source = ls.source;
				f.maxstacksize = 2;  /* registers 0/1 are always valid */
				enterblock (fs, bl, 0);
			}


			public static void close_func (LexState ls) {
				lua_State L = ls.L;
				FuncState fs = ls.fs;
				Proto f = fs.f;
				luaK_ret (fs, 0, 0);
				leaveblock (fs);
				luaM_reallocvector<uint> (L, ref f.code, f.sizecode, fs.pc);
				f.sizecode = fs.pc;
				luaM_reallocvector<int> (L, ref f.lineinfo, f.sizelineinfo, fs.pc);
				f.sizelineinfo = fs.pc;
				luaM_reallocvector<TValue> (L, ref f.k, f.sizek, fs.nk);
				f.sizek = fs.nk;
				luaM_reallocvector<Proto> (L, ref f.p, f.sizep, fs.np);
				f.sizep = fs.np;
				luaM_reallocvector<LocVar> (L, ref f.locvars, f.sizelocvars, fs.nlocvars);
				f.sizelocvars = fs.nlocvars;
				luaM_reallocvector<Upvaldesc> (L, ref f.upvalues, f.sizeupvalues, fs.nups);
				f.sizeupvalues = fs.nups;
				lua_assert (fs.bl == null);
				ls.fs = fs.prev;
				luaC_checkGC (L);
			}



			/*============================================================*/
			/* GRAMMAR RULES */
			/*============================================================*/
			
			
			/*
			** check whether current token is in the follow set of a block.
			** 'until' closes syntactical blocks, but do not close scope,
			** so it is handled in separate.
			*/
			public static bool block_follow (LexState ls, bool withuntil) {
				switch (ls.t.token) {
                case (int)RESERVED.TK_ELSE: goto case (int)RESERVED.TK_EOS;
                case (int)RESERVED.TK_ELSEIF: goto case (int)RESERVED.TK_EOS;
                case (int)RESERVED.TK_END: goto case (int)RESERVED.TK_EOS;
                case (int)RESERVED.TK_EOS:
					return true;
                case (int)RESERVED.TK_UNTIL: return true;
				default: return false;
				}
			}


			public static void statlist (LexState ls) {
				/* statlist -> { stat [';'] } */
				while (block_follow (ls, true) == false) {
                    if (ls.t.token == (int)RESERVED.TK_RETURN) {
						statement (ls);
						return;  /* 'return' must be last statement */
					}
					statement (ls);
				}
			}


			public static void fieldsel (LexState ls, expdesc v) {
				/* fieldsel -> ['.' | ':'] NAME */
				FuncState fs = ls.fs;
				expdesc key = new expdesc ();
				luaK_exp2anyregup (fs, v);
				luaX_next (ls);  /* skip the dot or colon */
				checkname (ls, key);
				luaK_indexed (fs, v, key);
			}


			public static void yindex (LexState ls, expdesc v) {
				/* index -> '[' expr ']' */
				luaX_next (ls);  /* skip the '[' */
				expr (ls, v);
				luaK_exp2val (ls.fs, v);
				checknext (ls, ']');
			}



			/*
			** {======================================================================
			** Rules for Constructors
			** =======================================================================
			*/


			public class ConsControl {
				public expdesc v;  /* last list item read */
				public expdesc t;  /* table descriptor */
				public int nh;  /* total number of 'record' elements */
				public int na;  /* total number of array elements */
				public int tostore;  /* number of array elements pending to be stored */

				public ConsControl () {
					v = new expdesc ();
				}
			}


			public static void recfield (LexState ls, ConsControl cc) {
				/* recfield -> (NAME | '['exp1']') = exp1 */
				FuncState fs = ls.fs;
				int reg = ls.fs.freereg;
				expdesc key = new expdesc ();
				expdesc val = new expdesc ();
                if (ls.t.token == (int)RESERVED.TK_NAME) {
					checklimit (fs, cc.nh, MAX_INT, "items in a constructor");
					checkname (ls, key);
				}
				else  /* ls->t.token == '[' */
					yindex (ls, key);
				cc.nh++;
				checknext (ls, '=');
				int rkkey = luaK_exp2RK (fs, key);
				expr (ls, val);
				luaK_codeABC (fs, OpCode.OP_SETTABLE, cc.t.u.info, rkkey, luaK_exp2RK (fs, val));
				fs.freereg = (byte)reg;  /* free registers */
			}


			public static void closelistfield (FuncState fs, ConsControl cc) {
				if (cc.v.k == expkind.VVOID) return;  /* there is no list item */
				luaK_exp2nextreg (fs, cc.v);
				cc.v.k = expkind.VVOID;
				if (cc.tostore == LFIELDS_PER_FLUSH) {
					luaK_setlist (fs, cc.t.u.info, cc.na, cc.tostore);  /* flush */
					cc.tostore = 0;  /* no more items pending */
				}
			}


			public static void lastlistfield (FuncState fs, ConsControl cc) {
				if (cc.tostore == 0) return;
				if (hasmultret (cc.v.k)) {
					luaK_setmultret (fs, cc.v);
					luaK_setlist (fs, cc.t.u.info, cc.na, lua530.LUA_MULTRET);
					cc.na--;  /* do not count last expression (unknown number of elements) */
				}
				else {
					if (cc.v.k != expkind.VVOID)
						luaK_exp2nextreg (fs, cc.v);
					luaK_setlist (fs, cc.t.u.info, cc.na, cc.tostore);
				}
			}


			public static void listfield (LexState ls, ConsControl cc) {
				/* listfield -> exp */
				expr (ls, cc.v);
				checklimit (ls.fs, cc.na, MAX_INT, "items in a constructor");
				cc.na++;
				cc.tostore++;
			}


			public static void field (LexState ls, ConsControl cc) {
				/* field -> listfield | recfield */
				switch (ls.t.token) {
                case (int)RESERVED.TK_NAME: {  /* may be 'listfield' or 'recfield' */
					if (luaX_lookahead (ls) != '=')  /* expression? */
						listfield (ls, cc);
					else
						recfield (ls, cc);
					break;
				}
				case '[': {
					recfield (ls, cc);
					break;
				}
				default: {
					listfield (ls, cc);
					break;
				}
				}
			}


			public static void constructor (LexState ls, expdesc t) {
				/* constructor -> '{' [ field { sep field } [sep] ] '}'
     				sep -> ',' | ';' */
				FuncState fs = ls.fs;
				int line = ls.linenumber;
				int pc = luaK_codeABC (fs, OpCode.OP_NEWTABLE, 0, 0, 0);
				ConsControl cc = new ConsControl ();
				cc.na = 0;
				cc.nh = 0;
				cc.tostore = 0;
				cc.t = t;
				init_exp (t, expkind.VRELOCABLE, pc);
				init_exp (cc.v, expkind.VVOID, 0);  /* no value (yet) */
				luaK_exp2nextreg (ls.fs, t);  /* fix it at stack top */
				checknext (ls, '{');
				do {
					lua_assert (cc.v.k == expkind.VVOID || cc.tostore > 0);
					if (ls.t.token == '}') break;
					closelistfield (fs, cc);
					field (ls, cc);
				} while (testnext (ls, ',') || testnext (ls, ';'));
				check_match (ls, '}', '{', line);
				lastlistfield (fs, cc);
				SETARG_B (ref fs.f.code[pc], luaO_int2fb ((uint)cc.na));  /* set initial array size */
                SETARG_C (ref fs.f.code[pc], luaO_int2fb ((uint)cc.nh));  /* set initial table size */
			}




			public static void parlist (LexState ls) {
				/* parlist -> [ param { ',' param } ] */
				FuncState fs = ls.fs;
				Proto f = fs.f;
				int nparams = 0;
				f.is_vararg = 0;
				if (ls.t.token != ')') {  /* is 'parlist' not empty? */
					do {
						switch (ls.t.token) {
						case (int)RESERVED.TK_NAME: {  /* param -> NAME */
							new_localvar (ls, str_checkname (ls));
							nparams++;
							break;
						}
                        case (int)RESERVED.TK_DOTS: {  /* param -> '...' */
							luaX_next (ls);
                            f.is_vararg = 1;
                            break;
						}
						default: {
							luaX_syntaxerror (ls, "<name> or '...' expected");
							break;
						}
						}
					} while (f.is_vararg == 0 && testnext (ls, ','));
				}
				adjustlocalvars (ls, nparams);
				f.numparams = (byte)fs.nactvar;
				luaK_reserveregs (fs, fs.nactvar);  /* reserve register for parameters */
			}


			public static void body (LexState ls, expdesc e, bool ismethod, int line) {
				/* body ->  '(' parlist ')' block END */
				FuncState new_fs = new FuncState ();
				BlockCnt bl = new BlockCnt ();
				new_fs.f = addprototype (ls);
				new_fs.f.linedefined = line;
				open_func (ls, new_fs, bl);
				checknext (ls, '(');
				if (ismethod) {
					new_localvalliteral (ls, "self");  /* create 'self' parameter */
					adjustlocalvars (ls, 1);
				}
				parlist (ls);
				checknext (ls, ')');
				statlist (ls);
				new_fs.f.lastlinedefined = ls.linenumber;
                check_match (ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FUNCTION, line);
				codeclosure (ls, e);
				close_func (ls);
			}


			public static int explist (LexState ls, expdesc v) {
				/* explist -> expr { ',' expr } */
				int n = 1;  /* at least one expression */
				expr (ls, v);
				while (testnext (ls, ',')) {
					luaK_exp2nextreg (ls.fs, v);
					expr (ls, v);
					n++;
				}
				return n;
			}


			public static void funcargs (LexState ls, expdesc f, int line) {
				FuncState fs = ls.fs;
				expdesc args = new expdesc ();
				int nbase = 0;
				int nparams = 0;
				switch (ls.t.token) {
				case '(': {  /* funcargs -> '(' [ explist ] ')' */
					luaX_next (ls);
					if (ls.t.token == ')')  /* arg list is empty? */
						args.k = expkind.VVOID;
					else {
						explist (ls, args);
						luaK_setmultret (fs, args);
					}
					check_match (ls, ')', '(', line);
					break;
				}
				case '{': {  /* funcargs -> constructor */
					constructor (ls, args);
					break;
				}
                case (int)RESERVED.TK_STRING: {  /* funcargs -> STRING */
					codestring (ls, args, (TString)ls.t.seminfo.o);
					luaX_next (ls);  /* must use 'seminfo' before 'next' */
					break;
				}
				default: {
					luaX_syntaxerror (ls, "function arguments expected");
					break;
				}
				}
				lua_assert (f.k == expkind.VNONRELOC);
				nbase = f.u.info;  /* base register for call */
				if (hasmultret (args.k))
					nparams = lua530.LUA_MULTRET;  /* open call */
				else {
					if (args.k != expkind.VVOID)
						luaK_exp2nextreg (fs, args);  /* close last argument */
					nparams = fs.freereg - (nbase + 1);
				}
				init_exp (f, expkind.VCALL, luaK_codeABC (fs, OpCode.OP_CALL, nbase, nparams + 1, 2));
				luaK_fixline (fs, line);
				fs.freereg = (byte)(nbase + 1);  /* call remove function and arguments and leaves
                           				 	(unless changed) one result */
			}



			/*
			** {======================================================================
			** Expression parsing
			** =======================================================================
			*/


			public static void primaryexp (LexState ls, expdesc v) {
				/* primaryexp -> NAME | '(' expr ')' */
				switch (ls.t.token) {
				case '(': {
					int line = ls.linenumber;
					luaX_next (ls);
					expr (ls, v);
					check_match (ls, ')', '(', line);
					luaK_dischargevars (ls.fs, v);
					return;
				}
				case (int)RESERVED.TK_NAME: {
					singlevar (ls, v);
					return;
				}
				default: {
					luaX_syntaxerror (ls, "unexpected symbol");
					break;
				}
				}
			}


			public static void suffixedexp (LexState ls, expdesc v) {
				/* suffixedexp ->
       				primaryexp { '.' NAME | '[' exp ']' | ':' NAME funcargs | funcargs } */
				FuncState fs = ls.fs;
				int line = ls.linenumber;
				primaryexp (ls, v);
				while (true) {
					switch (ls.t.token) {
					case '.': {  /* fieldsel */
						fieldsel (ls, v);
						break;
					}
					case '[': {  /* '[' exp1 ']' */
						expdesc key = new expdesc();
						luaK_exp2anyregup (fs, v);
						yindex (ls, key);
						luaK_indexed (fs, v, key);
						break;
					}
					case ':': {  /* ':' NAME funcargs */
						expdesc key = new expdesc();
						luaX_next (ls);
						checkname (ls, key);
						luaK_self (fs, v, key);
						funcargs (ls, v, line);
						break;
					}
					case '(': goto case '{';
                    case (int)RESERVED.TK_STRING: goto case '{';
					case '{': {  /* funcargs */
						luaK_exp2nextreg (fs, v);
						funcargs (ls, v, line);
						break;
					}
					default: return;
					}
				}
			}


			public static void simpleexp (LexState ls, expdesc v) {
				/* simpleexp -> FLT | INT | STRING | NIL | TRUE | FALSE | ... |
                  constructor | FUNCTION body | suffixedexp */
				switch (ls.t.token) {
                    case (int)RESERVED.TK_FLT: {
					init_exp (v, expkind.VKFLT, 0);
					v.u.nval = (double)ls.t.seminfo.o;
					break;
				}
                    case (int)RESERVED.TK_INT: {
					init_exp (v, expkind.VKINT, 0);
					v.u.ival = (long)ls.t.seminfo.o;
					break;
				}
                    case (int)RESERVED.TK_STRING: {
					codestring (ls, v, (TString)ls.t.seminfo.o);
					break;
				}
                    case (int)RESERVED.TK_NIL: {
					init_exp (v, expkind.VNIL, 0);
					break;
				}
                    case (int)RESERVED.TK_TRUE: {
					init_exp (v, expkind.VTRUE, 0);
					break;
				}
                    case (int)RESERVED.TK_FALSE: {
					init_exp (v, expkind.VFALSE, 0);
					break;
				}
                    case (int)RESERVED.TK_DOTS: {  /* vararg */
					FuncState fs = ls.fs;
					check_condition (ls, fs.f.is_vararg != 0, "cannot use '...' outside a vararg function");
					init_exp (v, expkind.VVARARG, luaK_codeABC (fs, OpCode.OP_VARARG, 0, 1, 0));
					break;
				}
				case '{': {  /* constructor */
					constructor (ls, v);
					return;
				}
                case (int)RESERVED.TK_FUNCTION: {
					luaX_next (ls);
					body (ls, v, false, ls.linenumber);
					return;
				}
				default: {
					suffixedexp (ls, v);
					return;
				}
				}
			}


			public static UnOpr getunopr (int op) {
				switch (op) {
                    case (int)RESERVED.TK_NOT: return UnOpr.OPR_NOT;
				case '-': return UnOpr.OPR_MINUS;
				case '~': return UnOpr.OPR_BNOT;
				case '#': return UnOpr.OPR_LEN;
				default: return UnOpr.OPR_NOUNOPR;
				}
			}
			
			
			public static BinOpr getbinopr (int op) {
				switch (op) {
				case '+': return BinOpr.OPR_ADD;
				case '-': return BinOpr.OPR_SUB;
				case '*': return BinOpr.OPR_MUL;
				case '%': return BinOpr.OPR_MOD;
				case '^': return BinOpr.OPR_POW;
				case '/': return BinOpr.OPR_DIV;
                case (int)RESERVED.TK_IDIV: return BinOpr.OPR_IDIV;
				case '&': return BinOpr.OPR_BAND;
				case '|': return BinOpr.OPR_BOR;
				case '~': return BinOpr.OPR_BXOR;
                case (int)RESERVED.TK_SHL: return BinOpr.OPR_SHL;
                case (int)RESERVED.TK_SHR: return BinOpr.OPR_SHR;
                case (int)RESERVED.TK_CONCAT: return BinOpr.OPR_CONCAT;
                case (int)RESERVED.TK_NE: return BinOpr.OPR_NE;
                case (int)RESERVED.TK_EQ: return BinOpr.OPR_EQ;
				case '<': return BinOpr.OPR_LT;
                case (int)RESERVED.TK_LE: return BinOpr.OPR_LE;
				case '>': return BinOpr.OPR_GT;
                case (int)RESERVED.TK_GE: return BinOpr.OPR_GE;
                case (int)RESERVED.TK_AND: return BinOpr.OPR_AND;
                case (int)RESERVED.TK_OR: return BinOpr.OPR_OR;
				default: return BinOpr.OPR_NOBINOPR;
				}
			}


            /* [n,0] */ /* left priority for each binary operator */
            /* [n,1] */ /* right priority */
            public static byte[,] priority =  {/* ORDER OPR */
               {10, 10}, {10, 10},           /* '+' '-' */
               {11, 11}, {11, 11},           /* '*' '%' */
               {14, 13},                  /* '^' (right associative) */
               {11, 11}, {11, 11},           /* '/' '//' */
               {6, 6}, {4, 4}, {5, 5},   /* '&' '|' '~' */
               {7, 7}, {7, 7},           /* '<<' '>>' */
               {9, 8},                   /* '..' (right associative) */
               {3, 3}, {3, 3}, {3, 3},   /* ==, <, <= */
               {3, 3}, {3, 3}, {3, 3},   /* ~=, >, >= */
               {2, 2}, {1, 1}            /* and, or */
            };

            public const int UNARY_PRIORITY = 12;  /* priority for unary operators */


            /*
            ** subexpr -> (simpleexp | unop subexpr) { binop subexpr }
            ** where 'binop' is any binary operator with a priority higher than 'limit'
            */
            public static BinOpr subexpr (LexState ls, expdesc v, int limit) {
                enterlevel (ls);
                UnOpr uop = getunopr (ls.t.token);
                if (uop != UnOpr.OPR_NOUNOPR) {
                    int line = ls.linenumber;
                    luaX_next (ls);
                    subexpr (ls, v, UNARY_PRIORITY);
                    luaK_prefix (ls.fs, uop, v, line);
                }
                else simpleexp (ls, v);
                /* expand while operators have priorities higher than 'limit' */
                BinOpr op = getbinopr (ls.t.token);
                while (op != BinOpr.OPR_NOBINOPR && priority[(int)op, 0] > limit) {
                    expdesc v2 = new expdesc ();
                    int line = ls.linenumber;
                    luaX_next (ls);
                    luaK_infix (ls.fs, op, v);
                    /* read sub-expression with higher priority */
                    BinOpr nextop = subexpr (ls, v2, priority[(int)op, 1]);
                    luaK_posfix (ls.fs, op, v, v2, line);
                    op = nextop;
                }
                leavelevel (ls);
                return op;  /* return first untreated operator */
            }


            public static void expr (LexState ls, expdesc v) {
                subexpr (ls, v, 0);
            }




            /*
            ** {======================================================================
            ** Rules for Statements
            ** =======================================================================
            */


            public static void block (LexState ls) {
                /* block -> statlist */
                FuncState fs = ls.fs;
                BlockCnt bl = new BlockCnt ();
                enterblock (fs, bl, 0);
                statlist (ls);
                leaveblock (fs);
            }


            /*
            ** structure to chain all variables in the left-hand side of an
            ** assignment
            */
            public class LHS_assign {
                public LHS_assign prev;
                public expdesc v;  /* variable (global, local, upvalue, or indexed) */
                public LHS_assign () {
                    v = new expdesc ();
                }
            }


            /*
            ** check whether, in an assignment to an upvalue/local variable, the
            ** upvalue/local variable is begin used in a previous assignment to a
            ** table. If so, save original upvalue/local value in a safe place and
            ** use this safe copy in the previous assignment.
            */
            public static void check_conflict (LexState ls, LHS_assign lh, expdesc v) {
                FuncState fs = ls.fs;
                byte extra = fs.freereg;  /* eventual position to save local variable */
                bool conflict = false;
                for (; lh != null; lh = lh.prev) {  /* check all previous assignments */
                    if (lh.v.k == expkind.VINDEXED) {  /* assigning to a table? */
                        /* table is the upvalue/local being assigned now? */
                        if (lh.v.u.ind.vt == v.k && lh.v.u.ind.t == v.u.info) {
                            conflict = true;
                            lh.v.u.ind.vt = expkind.VLOCAL;
                            lh.v.u.ind.t = extra;  /* previous assignment will use safe copy */
                        }
                        /* index is the local being assigned? (index cannot be upvalue) */
                        if (v.k == expkind.VLOCAL && lh.v.u.ind.idx == v.u.info) {
                            conflict = true;
                            lh.v.u.ind.idx = extra;  /* previous assignment will use safe copy */
                        }
                    }
                }
                if (conflict) {
                    /* copy upvalue/local value to a temporary (in position 'extra') */
                    OpCode op = (v.k == expkind.VLOCAL) ? OpCode.OP_MOVE : OpCode.OP_GETUPVAL;
                    luaK_codeABC (fs, op, extra, v.u.info, 0);
                    luaK_reserveregs (fs, 1);
                }
            }


            public static void assignment (LexState ls, LHS_assign lh, int nvars) {
                expdesc e = new expdesc ();
                check_condition (ls, vkisvar (lh.v.k), "syntax error");
                if (testnext (ls, ',')) {  /* assignment -> ',' suffixedexp assignment */
                    LHS_assign nv = new LHS_assign ();
                    nv.prev = lh;
                    suffixedexp (ls, nv.v);
                    if (nv.v.k != expkind.VINDEXED)
                        check_conflict (ls, lh, nv.v);
                    checklimit (ls.fs, nvars + ls.L.nCcalls, LUAI_MAXCCALLS, "C levels");
                    assignment (ls, nv, nvars + 1);
                }
                else {  /* assignment -> '=' explist */
                    checknext (ls, '=');
                    int nexps = explist (ls, e);
                    if (nexps != nvars) {
                        adjust_assign (ls, nvars, nexps, e);
                        if (nexps > nvars)
                            ls.fs.freereg -= (byte)(nexps - nvars);  /* remove extra values */
                    }
                    else {
                        luaK_setoneret (ls.fs, e);  /* close last expression */
                        luaK_storevar (ls.fs, lh.v, e);
                        return;  /* avoid default */
                    }
                }
                init_exp (e, expkind.VNONRELOC, ls.fs.freereg - 1);  /* default assignment */
                luaK_storevar (ls.fs, lh.v, e);
            }


            public static int cond (LexState ls) {
                /* cond -> exp */
                expdesc v = new expdesc ();
                expr (ls, v);  /* read condition */
                if (v.k == expkind.VNIL) v.k = expkind.VFALSE;  /* 'falses' are all equal here */
                luaK_goiftrue (ls.fs, v);
                return v.f;
            }


            public static void gotostat (LexState ls, int pc) {
                int line = ls.linenumber;
                TString label;
                if (testnext (ls, (int)RESERVED.TK_GOTO))
                    label = str_checkname (ls);
                else {
                    luaX_next (ls);  /* skip break */
                    label = luaS_new (ls.L, "break");
                }
                int g = newlabelentry (ls, ls.dyd.gt, label, line, pc);
                findlabel (ls, g);  /* close it if label already defined */
            }


            /* check for repeated labels on the same block */
            public static void checkrepeated (FuncState fs, Labellist ll, TString label) {
                for (int i = fs.bl.firstlabel; i < ll.n; i++) {
                    if (eqstr (label, ll.arr[i].name)) {
                        string msg = luaO_pushfstring (fs.ls.L, "label '%s' already defined on line %d", label.data, ll.arr[i].line);
                        semerror (fs.ls, msg);
                    }
                }
            }


            /* skip no-op statements */
            public static void skipnoopstat (LexState ls) {
                while (ls.t.token == ';' || ls.t.token == (int)RESERVED.TK_DBCOLON)
                    statement (ls);
            }


            public static void labelstat (LexState ls, TString label, int line) {
                /* label -> '::' NAME '::' */
                FuncState fs = ls.fs;
                Labellist ll = ls.dyd.label;
                checkrepeated (fs, ll, label);  /* check for repeated labels */
                checknext (ls, (int)RESERVED.TK_DBCOLON);  /* skip double colon */
                /* create new entry for this label */
                int l = newlabelentry (ls, ll, label, line, fs.pc);  /* index of new label being created */
                skipnoopstat (ls);  /* skip other no-op statements */
                if (block_follow (ls, false)) {  /* label is last no-op statement in the block? */
                    /* assume that locals are already out of scope */
                    ll.arr[l].nactvar = fs.bl.nactvar;
                }
                findgotos (ls, ll.arr[l]);
            }


            public static void whilestat (LexState ls, int line) {
                /* whilestat -> WHILE cond DO block END */
                FuncState fs = ls.fs;
                BlockCnt bl = new BlockCnt ();
                luaX_next (ls);  /* skip WHILE */
                int whileinit = luaK_getlabel (fs);
                int condexit = cond (ls);
                enterblock (fs, bl, 1);
                checknext (ls, (int)RESERVED.TK_DO);
                block (ls);
                luaK_jumpto (fs, whileinit);
                check_match (ls, (int)RESERVED.TK_END, (int)RESERVED.TK_WHILE, line);
                leaveblock (fs);
                luaK_patchtohere (fs, condexit);  /* false conditions finish the loop */
            }


            public static void repeatstat (LexState ls, int line) {
                /* repeatstat -> REPEAT block UNTIL cond */
                FuncState fs = ls.fs;
                int repeat_init = luaK_getlabel (fs);
                BlockCnt bl1 = new BlockCnt ();
                BlockCnt bl2 = new BlockCnt ();
                enterblock (fs, bl1, 1);  /* loop block */
                enterblock (fs, bl2, 0);  /* scope block */
                luaX_next (ls);  /* skip REPEAT */
                statlist (ls);
                check_match (ls, (int)RESERVED.TK_UNTIL, (int)RESERVED.TK_REPEAT, line);
                int condexit = cond (ls);  /* read condition (inside scope block) */
                if (bl2.upval != 0)  /* upvalues? */
                    luaK_patchclose (fs, condexit, bl2.nactvar);
                leaveblock (fs);  /* finish scope */
                luaK_patchlist (fs, condexit, repeat_init);  /* close the loop */
                leaveblock (fs);  /* finish loop */
            }


            public static int exp1 (LexState ls) {
                expdesc e = new expdesc ();
                expr (ls, e);
                luaK_exp2nextreg (ls.fs, e);
                lua_assert (e.k == expkind.VNONRELOC);
                int reg = e.u.info;
                return reg;
            }


            public static void forbody (LexState ls, int nbase, int line, int nvars, bool isnum) {
                /* forbody -> DO block */
                BlockCnt bl = new BlockCnt();
                FuncState fs = ls.fs;
                adjustlocalvars (ls, 3);  /* control variables */
                checknext (ls, (int)RESERVED.TK_DO);
                int prep = isnum ? luaK_codeAsBx (fs, OpCode.OP_FORPREP, nbase, NO_JUMP) : luaK_jump (fs);
                enterblock (fs, bl, 0);  /* scope for declared variables */
                adjustlocalvars (ls, nvars);
                luaK_reserveregs (fs, nvars);
                block (ls);
                leaveblock (fs);  /* end of scope for declared variables */
                luaK_patchtohere (fs, prep);
                int endfor = 0;
                if (isnum)  /* numeric for? */
                    endfor = luaK_codeAsBx (fs, OpCode.OP_FORLOOP, nbase, NO_JUMP);
                else {  /* generic for */
                    luaK_codeABC (fs, OpCode.OP_TFORCALL, nbase, 0, nvars);
                    luaK_fixline (fs, line);
                    endfor = luaK_codeAsBx (fs, OpCode.OP_TFORLOOP, nbase + 2, NO_JUMP);
                }
                luaK_patchlist (fs, endfor, prep + 1);
                luaK_fixline (fs, line);
            }


            public static void fornum (LexState ls, TString varname, int line) {
                /* fornum -> NAME = exp1,exp1[,exp1] forbody */
                FuncState fs = ls.fs;
                int nbase = fs.freereg;
                new_localvalliteral (ls, "(for index)");
                new_localvalliteral (ls, "(for limit)");
                new_localvalliteral (ls, "(for step)");
                new_localvar (ls, varname);
                checknext (ls, '=');
                exp1 (ls);
                checknext (ls, ',');
                exp1 (ls);
                if (testnext (ls, ','))
                    exp1 (ls);
                else {
                    luaK_codek (fs, fs.freereg, luaK_intK (fs, 1));
                    luaK_reserveregs (fs, 1);
                }
                forbody (ls, nbase, line, 1, true);
            }


            public static void forlist (LexState ls, TString indexname) {
                /* forlist -> NAME {,NAME} IN explist forbody */
                FuncState fs = ls.fs;
                expdesc e = new expdesc ();
                int nvars = 4;  /* gen, state, control, plus at least one declared var */
                int nbase = fs.freereg;
                /* create control variables */
                new_localvalliteral (ls, "(for generator)");
                new_localvalliteral (ls, "(for state)");
                new_localvalliteral (ls, "(for control)");
                /* create declared variables */
                new_localvar (ls, indexname);
                while (testnext (ls, ',')) {
                    new_localvar (ls, str_checkname (ls));
                    nvars++;
                }
                checknext (ls, (int)RESERVED.TK_IN);
                int line = ls.linenumber;
                adjust_assign (ls, 3, explist (ls, e), e);
                luaK_checkstack (fs, 3);
                /* extra space to call generator */
                forbody (ls, nbase, line, nvars - 3, false);
            }


            public static void forstat (LexState ls, int line) {
                /* forstat -> FOR (fornum | forlist) END */
                FuncState fs = ls.fs;
                BlockCnt bl = new BlockCnt ();
                enterblock (fs, bl, 1);  /* scope for loop and control variables */
                luaX_next (ls);  /* skip 'for' */
                TString varname = str_checkname (ls);  /* first variable name */
                switch (ls.t.token) {
                    case '=': {
                        fornum (ls, varname, line);
                        break;
                    }
                    case ',': goto case (int)RESERVED.TK_IN;
                    case (int)RESERVED.TK_IN: {
                        forlist (ls, varname);
                        break;
                    }
                    default: {
                        luaX_syntaxerror (ls, "'=' or 'in' expected");
                        break;
                    }
                }
                check_match (ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FOR, line);
                leaveblock (fs);  /* loop scope ('break' jumps to this point) */
            }


            public static void test_then_block (LexState ls, ref int escapelist) {
                /* test_then_block -> [IF | ELSEIF] cond THEN block */
                BlockCnt bl = new BlockCnt ();
                FuncState fs = ls.fs;
                expdesc v = new expdesc ();
                luaX_next (ls);  /* skip IF or ELSEIF */
                expr (ls, v);  /* read condition */
                checknext (ls, (int)RESERVED.TK_THEN);
                int jf = 0;  /* instruction to skip 'then' code (if condition is false) */
                if (ls.t.token == (int)RESERVED.TK_GOTO || ls.t.token == (int)RESERVED.TK_BREAK) {
                    luaK_goiffalse (ls.fs, v);  /* will jump to label if condition is true */
                    enterblock (fs, bl, 0);  /* must enter block before 'goto' */
                    gotostat (ls, v.t);  /* handle goto/break */
                    skipnoopstat (ls);  /* skip other no-op statements */
                    if (block_follow (ls, false)) {  /* 'goto' is the entire block? */
                        leaveblock (fs);
                        return;  /* and that is it */
                    }
                    else  /* must skip over 'then' part if condition is false */
                        jf = luaK_jump (fs);
                }
                else {  /* regular case (not goto/break) */
                    luaK_goiftrue (ls.fs, v);  /* skip over block if condition is false */
                    enterblock (fs, bl, 0);
                    jf = v.f;
                }
                statlist (ls);  /* 'then' part */
                leaveblock (fs);
                if (ls.t.token == (int)RESERVED.TK_ELSE || ls.t.token == (int)RESERVED.TK_ELSEIF)  /* followed by 'else'/'elseif'? */
                    luaK_concat (fs, ref escapelist, luaK_jump (fs));  /* must jump over it */
                luaK_patchtohere (fs, jf);
            }


            public static void ifstat (LexState ls, int line) {
                /* ifstat -> IF cond THEN block {ELSEIF cond THEN block} [ELSE block] END */
                FuncState fs = ls.fs;
                int escapelist = NO_JUMP;  /* exit list for finished parts */
                test_then_block (ls, ref escapelist);  /* IF cond THEN block */
                while (ls.t.token == (int)RESERVED.TK_ELSEIF)
                    test_then_block (ls, ref escapelist);  /* ELSEIF cond THEN block */
                if (testnext (ls, (int)RESERVED.TK_ELSE))
                    block (ls);  /* 'else' part */
                check_match (ls, (int)RESERVED.TK_END, (int)RESERVED.TK_IF, line);
                luaK_patchtohere (fs, escapelist);  /* patch escape list to 'if' end */
            }


            public static void localfunc (LexState ls) {
                expdesc b = new expdesc ();
                FuncState fs = ls.fs;
                new_localvar (ls, str_checkname (ls));  /* new local variable */
                adjustlocalvars (ls, 1);  /* enter its scope */
                body (ls, b, false, ls.linenumber);  /* function created in next register */
                /* debug information will only see the variable after this point! */
                getlocvar (fs, b.u.info).startpc = fs.pc;
            }


            public static void localstat (LexState ls) {
                /* stat -> LOCAL NAME {',' NAME} ['=' explist] */
                int nvars = 0;
                expdesc e = new expdesc ();
                do {
                    new_localvar (ls, str_checkname (ls));
                    nvars++;
                } while (testnext (ls, ','));
                int nexps = 0;
                if (testnext (ls, '='))
                    nexps = explist (ls, e);
                else {
                    e.k = expkind.VVOID;
                }
                adjust_assign (ls, nvars, nexps, e);
                adjustlocalvars (ls, nvars);
            }


            public static bool funcname (LexState ls, expdesc v) {
                /* funcname -> NAME {fieldsel} [':' NAME] */
                bool ismethod = false;
                singlevar (ls, v);
                while (ls.t.token == '.')
                    fieldsel (ls, v);
                if (ls.t.token == ':') {
                    ismethod = true;
                    fieldsel (ls, v);
                }
                return ismethod;
            }


            public static void funcstat (LexState ls, int line) {
                /* funcstat -> FUNCTION funcname body */
                expdesc v = new expdesc ();
                expdesc b = new expdesc ();
                luaX_next (ls);  /* skip FUNCTION */
                bool ismethod = funcname (ls, v);
                body (ls, b, ismethod, line);
                luaK_storevar (ls.fs, v, b);
                luaK_fixline (ls.fs, line);  /* definition "happens" in the first line */
            }


            public static void exprstat (LexState ls) {
                /* stat -> func | assignment */
                FuncState fs = ls.fs;
                LHS_assign v = new LHS_assign ();
                suffixedexp (ls, v.v);
                if (ls.t.token == '=' || ls.t.token == ',') {  /* stat -> assignment ? */
                    v.prev = null;
                    assignment (ls, v, 1);
                }
                else {  /* stat -> func */
                    check_condition (ls, v.v.k == expkind.VCALL, "syntax error");
                    SETARG_C (fs, v.v.u.info, 1);  /* call statement uses no results */
                }
            }


            public static void retstat (LexState ls) {
                /* stat -> RETURN [explist] [';'] */
                FuncState fs = ls.fs;
                expdesc e = new expdesc ();
                int first = 0;
                int nret = 0;  /* registers with returned values */
                if (block_follow (ls, true) || ls.t.token == ';')
                    nret = 0;  /* return no values */
                else {
                    nret = explist (ls, e);  /* optional return values */
                    if (hasmultret (e.k)) {
                        luaK_setmultret (fs, e);
                        if (e.k == expkind.VCALL && nret == 1) {
                            SET_OPCODE (fs, e.u.info, (int)OpCode.OP_TAILCALL);  /* tail call? */
                            lua_assert (GETARG_A (fs, e.u.info) == fs.nactvar);
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
                        check_match (ls, (int)RESERVED.TK_END, (int)RESERVED.TK_DO, line);
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
                        if (testnext (ls, (int)RESERVED.TK_FUNCTION))  /* local function? */
                            localfunc (ls);
                        else                              
                            localstat (ls);
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
                check (ls, (int)RESERVED.TK_EOS);
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
                    public expkind vt;  /* whether 't' is register (VLOCAL) or upvalue (VUPVAL) */
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

            public void copy (expdesc o) {
                t = o.t;
                f = o.f;
                k = o.k;
                u.ind.idx = o.u.ind.idx;
                u.ind.t = o.u.ind.t;
                u.ind.vt = o.u.ind.vt;
                u.info = o.u.info;
                u.nval = o.u.nval;
                u.ival = o.u.ival;
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

			public void copy (Labeldesc o) {
				name = o.name;
				pc = o.pc;
				line = o.line;
				nactvar = o.nactvar;
			}
        }


        /* list of labels or gotos */
        public class Labellist {
            public Labeldesc[] arr;  /* array */
            public int n;  /* number of entries in use */
            public int size;  /* array size */
        }


        /* dynamic structures used by the parser */
        public class Dyndata {
            public class ca {
                public Vardesc[] arr;
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