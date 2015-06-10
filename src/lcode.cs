using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lcode {

            /* Maximum number of registers in a Lua function */
            public const int MAXREGS = 250;


            public static bool hasjumps (expdesc e) { return (e.t != e.f); }


            public static bool tonumeral (expdesc e, TValue v) {
                if (e.t != NO_JUMP || e.f != NO_JUMP)
                    return false;
                switch (e.k) {
                    case expkind.VKINT:
                        if (v != null) setivalue (v, e.u.ival);
                        return true;
                    case expkind.VKFLT:
                        if (v != null) setfltvalue (v, e.u.nval);
                        return true;
                    default: return false;
                }
            }


            public static int condjump (FuncState fs, OpCode op, int A, int B, int C) {
                luaK_codeABC (fs, op, A, B, C);
                return luaK_jump (fs);
            }


            public static void fixjump (FuncState fs, int pc, int dest) {
                int offset = dest - (pc + 1);
                lua_assert (dest != NO_JUMP);
                if (Math.Abs (offset) > MAXARG_sBx)
                    luaX_syntaxerror (fs.ls, "control structure too long");
                SETARG_sBx (fs, pc, offset);
            }


            public static int getjump (FuncState fs, int pc) {
                int offset = GETARG_sBx (fs.f.code[pc]);
                if (offset == NO_JUMP)  /* point to itself represents end of list */
                    return NO_JUMP;  /* end of list */
                else
                    return (pc + 1) + offset;  /* turn offset into absolute position */
            }


            public static int getjumpcontrol (FuncState fs, int pc) {
                if (pc >= 1 && testTMode (GET_OPCODE (fs.f.code[pc - 1])))
                    return pc - 1;
                else
                    return pc;
            }


            /*
            ** check whether list has any jump that do not produce a value
            ** (or produce an inverted value)
            */
            public static bool need_value (FuncState fs, int list) {
                for (; list != NO_JUMP; list = getjump (fs, list)) {
                    uint i = fs.f.code[getjumpcontrol (fs, list)];
                    if (GET_OPCODE (i) != OpCode.OP_TESTSET) return true;
                }
                return false;
            }


            public static bool patchtestreg (FuncState fs, int node, int reg) {
                int i = getjumpcontrol (fs, node);
                uint pi = fs.f.code[i];
                if (GET_OPCODE (pi) != OpCode.OP_TESTSET)
                    return false;
                if (reg != NO_REG && reg != GETARG_B (pi))
                    SETARG_A (fs, i, reg);
                else
                    fs.f.code[i] = CREATE_ABC (OpCode.OP_TEST, GETARG_B (pi), 0, GETARG_C (pi));
                return true;
            }


            public static void removevalues (FuncState fs, int list) {
                for (; list != NO_JUMP; list = getjump (fs, list))
                    patchtestreg (fs, list, NO_REG);
            }


            public static void patchlistaux (FuncState fs, int list, int vtarget, int reg, int dtarget) {
                while (list != NO_JUMP) {
                    int next = getjump (fs, list);
                    if (patchtestreg (fs, list, reg))
                        fixjump (fs, list, vtarget);
                    else
                        fixjump (fs, list, dtarget);  /* jump to default target */
                    list = next;
                }
            }


            public static void dischargejpc (FuncState fs) {
                patchlistaux (fs, fs.jpc, fs.pc, NO_REG, fs.pc);
                fs.jpc = NO_JUMP;
            }


            public static int codeextraarg (FuncState fs, int a) {
                lua_assert (a <= MAXARG_Ax);
                return luaK_code (fs, CREATE_Ax (OpCode.OP_EXTRAARG, a));
            }


            public static void freereg (FuncState fs, int reg) {
                if (ISK (reg) == false && reg >= fs.nactvar) {
                    fs.freereg--;
                    lua_assert (reg == fs.freereg);
                }
            }


            public static void freeexp (FuncState fs, expdesc e) {
                if (e.k == expkind.VNONRELOC)
                    freereg (fs, e.u.info);
            }


            /*
            ** Use scanner's table to cache position of constants in constant list
            ** and try to reuse constants
            */
            public static int addk (FuncState fs, TValue key, TValue v) {
                lua_State L = fs.ls.L;
                Proto f = fs.f;
                TValue idx = luaH_set (L, fs.ls.h, key);
                int k = 0;
                if (ttisinteger (idx)) {
                    k = (int)ivalue (idx);
                    if (k < fs.nk && ttype (f.k[k]) == ttype (v) &&
                                        luaV_rawequalobj (f.k[k], v))
                        return k;
                }
                int oldsize = f.sizek;
                k = fs.nk;
                setivalue (idx, k);
                luaM_growvector<TValue> (L, ref f.k, k, f.sizek, MAXARG_Ax, "constants");
                while (oldsize < f.sizek) setnilvalue (f.k[oldsize++]);
                setobj (L, f.k[k], v);
                fs.nk++;
                luaC_barrier (L, f, v);
                return k;
            }


            public static int boolK (FuncState fs, int b) {
                TValue o = new TValue ();
                setbvalue (o, b);
                return addk (fs, o, o);
            }


            public static int nilK (FuncState fs) {
                TValue k = new TValue ();
                TValue v = new TValue ();
                setnilvalue (v);
                /* cannot use nil as key; instead use table itself to represent nil */
                sethvalue (fs.ls.L, k, fs.ls.h);
                return addk (fs, k, v);
            }


            public static int code_label (FuncState fs, int A, int b, int jump) {
                luaK_getlabel (fs);  /* those instructions may be jump targets */
                return luaK_codeABC (fs, OpCode.OP_LOADBOOL, A, b, jump);
            }


            public static void discharge2reg (FuncState fs, expdesc e, int reg) {
                luaK_dischargevars (fs, e);
                switch (e.k) {
                    case expkind.VNIL: {
                        luaK_nil (fs, reg, 1);
                        break;
                    }
                    case expkind.VFALSE: goto case expkind.VTRUE;
                    case expkind.VTRUE: {
                        luaK_codeABC (fs, OpCode.OP_LOADBOOL, reg, (e.k == expkind.VTRUE ? 1 : 0), 0);
                        break;
                    }
                    case expkind.VK: {
                        luaK_codek (fs, reg, e.u.info);
                        break;
                    }
                    case expkind.VKFLT: {
                        luaK_codek (fs, reg, luaK_numberK (fs, e.u.nval));
                        break;
                    }
                    case expkind.VKINT: {
                        luaK_codek (fs, reg, luaK_intK (fs, e.u.ival));
                        break;
                    }
                    case expkind.VRELOCABLE: {
                        SETARG_A (fs, e.u.info, reg);
                        break;
                    }
                    case expkind.VNONRELOC: {
                        if (reg != e.u.info)
                            luaK_codeABC (fs, OpCode.OP_MOVE, reg, e.u.info, 0);
                        break;
                    }
                    default: {
                        lua_assert (e.k == expkind.VVOID || e.k == expkind.VJMP);
                        return;  /* nothing to do... */
                    }
                }
                e.u.info = reg;
                e.k = expkind.VNONRELOC;
            }


            public static void discharge2anyreg (FuncState fs, expdesc e) {
                if (e.k != expkind.VNONRELOC) {
                    luaK_reserveregs (fs, 1);
                    discharge2reg (fs, e, fs.freereg - 1);
                }
            }


            public static void exp2reg (FuncState fs, expdesc e, int reg) {
                discharge2reg (fs, e, reg);
                if (e.k == expkind.VJMP)
                    luaK_concat (fs, ref e.t, e.u.info);
                if (hasjumps (e)) {
                    int p_f = NO_JUMP;  /* position of an eventual LOAD false */
                    int p_t = NO_JUMP;  /* position of an eventual LOAD true */
                    if (need_value (fs, e.t) || need_value (fs, e.f)) {
                        int fj = (e.k == expkind.VJMP) ? NO_JUMP : luaK_jump (fs);
                        p_f = code_label (fs, reg, 0, 1);  
                        p_t = code_label (fs, reg, 1, 0);
                        luaK_patchtohere (fs, fj);
                    }
                    int final = luaK_getlabel (fs);  /* position after whole expression */
                    patchlistaux (fs, e.f, final, reg, p_f);
                    patchlistaux (fs, e.t, final, reg, p_t);
                }
                e.f = NO_JUMP;
                e.t = NO_JUMP;
                e.u.info = reg;
                e.k = expkind.VNONRELOC;
            }


            public static void invertjump (FuncState fs, expdesc e) {
                int pc = getjumpcontrol (fs, e.u.info);
                lua_assert (testTMode (GET_OPCODE (fs, pc)) && GET_OPCODE (fs, pc) != OpCode.OP_TESTSET &&
                                                               GET_OPCODE (fs, pc) != OpCode.OP_TEST);
                SETARG_A (fs, pc, (GETARG_A (fs, pc) == 0 ? 1 : 0));
            }


            public static int jumponcond (FuncState fs, expdesc e, int cond) {
                if (e.k == expkind.VRELOCABLE) {
                    uint ie = fs.f.code[e.u.info];
                    if (GET_OPCODE (ie) == OpCode.OP_NOT) {
                        fs.pc--;
                        return condjump (fs, OpCode.OP_TEST, GETARG_B (ie), 0, (cond == 0 ? 1 : 0));
                    }
                }
                discharge2anyreg (fs, e);
                freeexp (fs, e);
                return condjump (fs, OpCode.OP_TEST, NO_REG, e.u.info, cond);
            }


            public static void codenot (FuncState fs, expdesc e) {
                luaK_dischargevars (fs, e);
                switch (e.k) {
                    case expkind.VNIL: goto case expkind.VFALSE;
                    case expkind.VFALSE: {
                        e.k = expkind.VTRUE;
                        break;
                    }
                    case expkind.VK: goto case expkind.VTRUE;
                    case expkind.VKFLT: goto case expkind.VTRUE;
                    case expkind.VKINT: goto case expkind.VTRUE;
                    case expkind.VTRUE: {
                        e.k = expkind.VFALSE;
                        break;
                    }
                    case expkind.VJMP: {
                        invertjump (fs, e);
                        break;
                    }
                    case expkind.VRELOCABLE: goto case expkind.VNONRELOC;
                    case expkind.VNONRELOC: {
                        discharge2anyreg (fs, e);
                        freeexp (fs, e);
                        e.u.info = luaK_codeABC (fs, OpCode.OP_NOT, 0, e.u.info, 0);
                        e.k = expkind.VRELOCABLE;
                        break;
                    }
                    default: {
                        lua_assert (false);  /* cannot happen */
                        break; 
                    }
                }
                /* interchange true and false lists */
                int temp = e.f;
                e.f = e.t;
                e.t = temp;
                removevalues (fs, e.f);
                removevalues (fs, e.t);
            }


            /*
            ** return false if folding can raise an error
            */
            public static bool validop (int op, TValue v1, TValue v2) {
                switch (op) {
                    case lua530.LUA_OPBAND: goto case lua530.LUA_OPBNOT;
                    case lua530.LUA_OPBOR: goto case lua530.LUA_OPBNOT;
                    case lua530.LUA_OPBXOR: goto case lua530.LUA_OPBNOT;
                    case lua530.LUA_OPSHL: goto case lua530.LUA_OPBNOT;
                    case lua530.LUA_OPSHR: goto case lua530.LUA_OPBNOT;
                    case lua530.LUA_OPBNOT: {  /* conversion errors */
                        long i = 0;
                        return (tointeger (v1, ref i) && tointeger (v2, ref i));
                    }
                    case lua530.LUA_OPDIV: goto case lua530.LUA_OPMOD;
                    case lua530.LUA_OPIDIV: goto case lua530.LUA_OPMOD;
                    case lua530.LUA_OPMOD: {  /* division by 0 */
                        return (nvalue (v2) != 0);
                    }
                    default: return true;  /* everything else is valid */
                }
            }


            /*
            ** Try to "constant-fold" an operation; return 1 if successful
            */
            public static bool constfolding (FuncState fs, int op, expdesc e1, expdesc e2) {
                TValue v1 = new TValue ();
                TValue v2 = new TValue ();
                TValue res = new TValue ();
                if (tonumeral (e1, v1) == false || tonumeral (e2, v2) == false || validop (op, v1, v2) == false)
                    return false;
                luaO_arith (fs.ls.L, op, v1, v2, res);
                if (ttisinteger (res)) {
                    e1.k = expkind.VKINT;
                    e1.u.ival = ivalue (res);
                }
                else {
                    double n = fltvalue (res);
                    if (luai_numisnan (n) || n == 0)
                        return false;
                    e1.k = expkind.VKFLT;
                    e1.u.nval = n;
                }
                return true;
            }


            /*
            ** Code for binary and unary expressions that "produce values"
            ** (arithmetic operations, bitwise operations, concat, length). First
            ** try to do constant folding (only for numeric [arithmetic and
            ** bitwise] operations, which is what 'lua_arith' accepts).
            ** Expression to produce final result will be encoded in 'e1'.
            */
            public static void codeexpval (FuncState fs, OpCode op, expdesc e1, expdesc e2, int line) {
                lua_assert (op >= OpCode.OP_ADD);
                if (op <= OpCode.OP_BNOT && constfolding (fs, op - OpCode.OP_ADD + lua530.LUA_OPADD, e1, e2))
                    return;  /* result has been folded */
                else {
                    int o1;
                    int o2;
                    /* move operands to registers (if needed) */
                    if (op == OpCode.OP_UNM || op == OpCode.OP_BNOT || op == OpCode.OP_LEN) {
                        o2 = 0;  /* no second expression */
                        o1 = luaK_exp2anyreg (fs, e1);  /* cannot operate on constants */
                    }
                    else {  /* regular case (binary operators) */
                        o2 = luaK_exp2RK (fs, e2);  /* both operands are "RK" */
                        o1 = luaK_exp2RK (fs, e1);
                    }
                    if (o1 > o2) {  /* free registers in proper order */
                        freeexp (fs, e1);
                        freeexp (fs, e2);
                    }
                    else {
                        freeexp (fs, e2);
                        freeexp (fs, e1);
                    }
                    e1.u.info = luaK_codeABC (fs, op, 0, o1, o2);  /* generate opcode */
                    e1.k = expkind.VRELOCABLE;  /* all those operations are relocable */
                    luaK_fixline (fs, line);
                }
            }


            public static void codecomp (FuncState fs, OpCode op, int cond, expdesc e1, expdesc e2) {
                int o1 = luaK_exp2RK (fs, e1);
                int o2 = luaK_exp2RK (fs, e2);
                freeexp (fs, e2);
                freeexp (fs, e1);
                if (cond == 0 && op != OpCode.OP_EQ) {
                    int temp = o1;
                    o1 = o2;
                    o2 = temp;
                    cond = 1;
                }
                e1.u.info = condjump (fs, op, cond, o1, o2);
                e1.k = expkind.VJMP;
            }































        }

        /*
        ** Marks the end of a patch list. It is an invalid value both as an absolute
        ** address, and as a list link (would link an element to itself).
        */
        public const int NO_JUMP = -1;


        /*
        ** grep "ORDER OPR" if you change these enums  (ORDER OP)
        */
        public enum BinOpr {
            OPR_ADD, OPR_SUB, OPR_MUL, OPR_MOD, OPR_POW,
            OPR_DIV,
            OPR_IDIV,
            OPR_BAND, OPR_BOR, OPR_BXOR,
            OPR_SHL, OPR_SHR,
            OPR_CONCAT,
            OPR_EQ, OPR_LT, OPR_LE,
            OPR_NE, OPR_GT, OPR_GE,
            OPR_AND, OPR_OR,
            OPR_NOBINOPR
        }

        public enum UnOpr { OPR_MINUS, OPR_BNOT, OPR_NOT, OPR_LEN, OPR_NOUNOPR }


        public static int luaK_codeAsBx (FuncState fs, OpCode o, int A, int sBx) { return luaK_codeABx (fs, o, A, sBx + MAXARG_sBx); }



        public static void luaK_nil (FuncState fs, int from, int n) {
            int l = from + n - 1;  /* last register to set nil */
            if (fs.pc > fs.lasttarget) {  /* no jumps to current position? */
                uint previous = fs.f.code[fs.pc - 1];
                if (GET_OPCODE (previous) == OpCode.OP_LOADNIL) {
                    int pfrom = GETARG_A (previous);
                    int pl = pfrom + GETARG_B (previous);
                    if ((pfrom <= from && from <= pl + l) ||
                        (from <= pfrom && pfrom <= l + 1)) {  /* can connect both? */
                        if (pfrom < from) from = pfrom;  /* from = min(from, pfrom) */
                        if (pl > l) l = pl;  /* l = max(l, pl) */
                        SETARG_A (ref fs.f.code[fs.pc - 1], from);
                        SETARG_B (ref fs.f.code[fs.pc - 1], 1 - from);
                        return;
                    }
                }  /* else go through */
            }
            luaK_codeABC (fs, OpCode.OP_LOADNIL, from, n - 1, 0);  /* else no optimization */
        }


        public static int luaK_jump (FuncState fs) {
            int jpc = fs.jpc;  /* save list of jumps to here */
            fs.jpc = NO_JUMP;
            int j = luaK_codeAsBx (fs, OpCode.OP_JMP, 0, NO_JUMP);
            luaK_concat (fs, ref j, jpc);  /* keep them on hold */
            return j;
        }


        public static void luaK_jumpto (FuncState fs, int t) { luaK_patchlist (fs, luaK_jump (fs), t); }


        public static void luaK_ret (FuncState fs, int first, int nret) {
            luaK_codeABC (fs, OpCode.OP_RETURN, first, nret + 1, 0);
        }


        /*
        ** returns current 'pc' and marks it as a jump target (to avoid wrong
        ** optimizations with consecutive instructions not in the same basic block).
        */
        public static int luaK_getlabel (FuncState fs) {
            fs.lasttarget = fs.pc;
            return fs.pc;
        }


        public static void luaK_patchlist (FuncState fs, int list, int target) {
            if (target == fs.pc)
                luaK_patchtohere (fs, list);
            else {
                lua_assert (target < fs.pc);
                lcode.patchlistaux (fs, list, target, NO_REG, target);
            }
        }


        public static void luaK_patchclose (FuncState fs, int list, int level) {
            level++;
            while (list != NO_JUMP) {
                int next = lcode.getjump (fs, list);
                lua_assert (GET_OPCODE (fs.f.code[list]) == OpCode.OP_JMP &&
                            (GETARG_A (fs.f.code[list]) == 0 ||
                             GETARG_A (fs.f.code[list]) >= level));
                SETARG_A (fs, list, level);
                list = next;
            }
        }


        public static void luaK_patchtohere (FuncState fs, int list) {
            luaK_getlabel (fs);
            luaK_concat (fs, ref fs.jpc, list);
        }


        public static void luaK_concat (FuncState fs, ref int l1, int l2) {
            if (l2 == NO_JUMP) return;
            else if (l1 == NO_JUMP)
                l1 = l2;
            else {
                int list = l1;
                int next = lcode.getjump (fs, list);
                while (next != NO_JUMP) {
                    list = next;
                    next = lcode.getjump (fs, next);
                }
                lcode.fixjump (fs, list, l2);
            }
        }


        public static int luaK_code (FuncState fs, uint i) {
            Proto f = fs.f;
            lcode.dischargejpc (fs);
            luaM_growvector<uint> (fs.ls.L, ref f.code, fs.pc, f.sizecode, MAX_INT, "opcodes");
            f.code[fs.pc] = i;
            luaM_growvector<int> (fs.ls.L, ref f.lineinfo, fs.pc, f.sizelineinfo, MAX_INT, "opcodes");
            f.lineinfo[fs.pc] = fs.ls.lastline;
            return fs.pc++;
        }


        public static int luaK_codeABC (FuncState fs, OpCode o, int a, int b, int c) {
            lua_assert (getOpMode (o) == OpMode.iABC);
            lua_assert (getBMode (o) != OpArgMask.OpArgN || b == 0);
            lua_assert (getCMode (o) != OpArgMask.OpArgN || c == 0);
            lua_assert (a <= MAXARG_A && b <= MAXARG_B && c <= MAXARG_C);
            return luaK_code (fs, CREATE_ABC (o, a, b, c));
        }


        public static int luaK_codeABx (FuncState fs, OpCode o, int a, int bc) {
            lua_assert (getOpMode (o) == OpMode.iABx || getOpMode (o) == OpMode.iAsBx);
            lua_assert (getCMode (o) == OpArgMask.OpArgN);
            lua_assert (a <= MAXARG_A && bc <= MAXARG_Bx);
            return luaK_code (fs, CREATE_ABx (o, a, bc));
        }


        public static int luaK_codek (FuncState fs, int reg, int k) {
            if (k <= MAXARG_Bx)
                return luaK_codeABx (fs, OpCode.OP_LOADK, reg, k);
            else {
                int p = luaK_codeABx (fs, OpCode.OP_LOADKX, reg, 0);
                lcode.codeextraarg (fs, k);
                return p;
            }
        }


        public static void luaK_checkstack (FuncState fs, int n) {
            int newstack = fs.freereg + n;
            if (newstack > fs.f.maxstacksize) {
                if (newstack >= lcode.MAXREGS)
                    luaX_syntaxerror (fs.ls, "function or expression too complex");
                fs.f.maxstacksize = (byte)newstack;
            }
        }


        public static void luaK_reserveregs (FuncState fs, int n) {
            luaK_checkstack (fs, n);
            fs.freereg += (byte)n;
        }


        public static int luaK_stringK (FuncState fs, TString s) {
            TValue o = new TValue ();
            setsvalue (fs.ls.L, o, s);
            return lcode.addk (fs, o, o);
        }


        /*
        ** Integers use userdata as keys to avoid collision with floats with same
        ** value; conversion to 'void*' used only for hashing, no "precision"
        ** problems
        */
        public static int luaK_intK (FuncState fs, long n) {
            TValue k = new TValue ();
            TValue o = new TValue ();
            setpvalue (k, n);
            setivalue (o, n);
            return lcode.addk (fs, k, o);
        }


        public static int luaK_numberK (FuncState fs, double r) {
            TValue o = new TValue ();
            setfltvalue (o, r);
            return lcode.addk (fs, o, o);
        }


        public static void luaK_setreturns (FuncState fs, expdesc e, int nresults) {
            if (e.k == expkind.VCALL) {  /* expression is an open function call? */
                SETARG_C (fs, e.u.info, nresults + 1);
            }
            else if (e.k == expkind.VVARARG) {
                SETARG_B (fs, e.u.info, nresults + 1);
                SETARG_A (fs, e.u.info, fs.freereg);
                luaK_reserveregs (fs, 1);
            }
        }


		public static void luaK_setmultret (FuncState fs, expdesc e) { luaK_setreturns (fs, e, lua530.LUA_MULTRET); }


        public static void luaK_setoneret (FuncState fs, expdesc e) {
            if (e.k == expkind.VCALL) {  /* expression is an open function call? */
                e.k = expkind.VNONRELOC;
                e.u.info = GETARG_A (fs, e.u.info);
            }
            else if (e.k == expkind.VVARARG) {
                SETARG_B (fs, e.u.info, 2);
                e.k = expkind.VRELOCABLE;  /* can relocate its simple result */
            }
        }


        public static void luaK_dischargevars (FuncState fs, expdesc e) {
            switch (e.k) {
                case expkind.VLOCAL: {
                    e.k = expkind.VNONRELOC;
                    break;
                }
                case expkind.VUPVAL: {
                    e.u.info = luaK_codeABC (fs, OpCode.OP_GETUPVAL, 0, e.u.info, 0);
                    e.k = expkind.VRELOCABLE;
                    break;
                }
                case expkind.VINDEXED: {
                    OpCode op = OpCode.OP_GETTABUP;  /* assume 't' is in an upvalue */
                    lcode.freereg (fs, e.u.ind.idx);
                    if (e.u.ind.vt == expkind.VLOCAL) {  /* 't' is in a register? */
                        lcode.freereg (fs, e.u.ind.t);
                        op = OpCode.OP_GETTABLE;
                    }
                    e.u.info = luaK_codeABC (fs, op, 0, e.u.ind.t, e.u.ind.idx);
                    e.k = expkind.VRELOCABLE;
                    break;
                }
                case expkind.VVARARG: goto case expkind.VCALL;
                case expkind.VCALL: {
                    luaK_setoneret (fs, e);
                    break;
                }
                default: break;  /* there is one value available (somewhere) */
            }
        }


        public static void luaK_exp2nextreg (FuncState fs, expdesc e) {
            luaK_dischargevars (fs, e);
            lcode.freeexp (fs, e);
            luaK_reserveregs (fs, 1);
            lcode.exp2reg (fs, e, fs.freereg - 1);
        }


        public static int luaK_exp2anyreg (FuncState fs, expdesc e) {
            luaK_dischargevars (fs, e);
            if (e.k == expkind.VNONRELOC) {
                if (lcode.hasjumps (e) == false) return e.u.info;  /* exp is already in a register */
                if (e.u.info >= fs.nactvar) {  /* reg. is not a local? */
                    lcode.exp2reg (fs, e, e.u.info);  /* put value on it */
                    return e.u.info;
                }
            }
            luaK_exp2nextreg (fs, e);  /* default */
            return e.u.info;
        }


        public static void luaK_exp2anyregup (FuncState fs, expdesc e) {
            if (e.k != expkind.VUPVAL || lcode.hasjumps (e))
                luaK_exp2anyreg (fs, e);
        }


        public static void luaK_exp2val (FuncState fs, expdesc e) {
            if (lcode.hasjumps (e))
                luaK_exp2anyreg (fs, e);
            else
                luaK_dischargevars (fs, e);
        }

        public static int luaK_exp2RK (FuncState fs, expdesc e) {
            luaK_exp2val (fs, e);
            switch (e.k) {
                case expkind.VTRUE: goto case expkind.VNIL;
                case expkind.VFALSE: goto case expkind.VNIL;
                case expkind.VNIL: {
                    if (fs.nk <= MAXINDEXRK) {
                        e.u.info = (e.k == expkind.VNIL) ? lcode.nilK (fs) : lcode.boolK (fs, (e.k == expkind.VTRUE ? 1 : 0));
                        e.k = expkind.VK;
                        return RKASK (e.u.info);
                    }
                    else break;
                }
                case expkind.VKINT: {
                    e.u.info = luaK_intK (fs, e.u.ival);
                    e.k = expkind.VK;
                    goto case expkind.VK;
                }
                case expkind.VKFLT: {
                    e.u.info = luaK_numberK (fs, e.u.nval);
                    e.k = expkind.VK;
                    /* go through */
                    goto case expkind.VK;
                }
                case expkind.VK: {
                    if (e.u.info <= MAXINDEXRK)  /* constant fits in 'argC'? */
                        return RKASK (e.u.info);
                    else break;
                }
                default: break;
            }
            /* not a constant in the right range: put it in a register */
            return luaK_exp2anyreg (fs, e);
        }


        public static void luaK_storevar (FuncState fs, expdesc var, expdesc ex) {
            switch (var.k) {
                case expkind.VLOCAL: {
                    lcode.freeexp (fs, ex);
                    lcode.exp2reg (fs, ex, var.u.info);
                    return;
                }
                case expkind.VUPVAL: {
                    int e = luaK_exp2anyreg (fs, ex);
                    luaK_codeABC (fs, OpCode.OP_SETUPVAL, e, var.u.info, 0);
                    break;
                }
                case expkind.VINDEXED: {
                    OpCode op = (var.u.ind.vt == expkind.VLOCAL) ? OpCode.OP_SETTABLE : OpCode.OP_SETTABUP;
                    int e = luaK_exp2RK (fs, ex);
                    luaK_codeABC (fs, op, var.u.ind.t, var.u.ind.idx, e);
                    break;
                }
                default: {
                    lua_assert (false);  /* invalid var kind to store */
                    break;
                }
            }
            lcode.freeexp (fs, ex);
        }


        public static void luaK_self (FuncState fs, expdesc e, expdesc key) {
            luaK_exp2anyreg (fs, e);
            int ereg = e.u.info;
            lcode.freeexp (fs, e);
            e.u.info = fs.freereg;
            e.k = expkind.VNONRELOC;
            luaK_reserveregs (fs, 2);
            luaK_codeABC (fs, OpCode.OP_SELF, e.u.info, ereg, luaK_exp2RK (fs, key));
            lcode.freeexp (fs, key);
        }


        public static void luaK_goiftrue (FuncState fs, expdesc e) {
            luaK_dischargevars (fs, e);
            int pc = 0;  /* pc of last jump */
            switch (e.k) {
                case expkind.VJMP: {
                    lcode.invertjump (fs, e);
                    pc = e.u.info;
                    break;
                }
                case expkind.VK: goto case expkind.VTRUE;
                case expkind.VKFLT: goto case expkind.VTRUE;
                case expkind.VKINT: goto case expkind.VTRUE;
                case expkind.VTRUE: {
                    pc = NO_JUMP;  /* always true; do nothing */
                    break;
                }
                default: {
                    pc = lcode.jumponcond (fs, e, 0);
                    break;
                }
            }
            luaK_concat (fs, ref e.f, pc);  /* insert last jump in 'f' list */
            luaK_patchtohere (fs, e.t);
            e.t = NO_JUMP;
        }


        public static void luaK_goiffalse (FuncState fs, expdesc e) {
            luaK_dischargevars (fs, e);
            int pc = 0;  /* pc of last jump */
            switch (e.k) {
                case expkind.VJMP: {
                    pc = e.u.info;
                    break;
                }
                case expkind.VNIL: goto case expkind.VFALSE;
                case expkind.VFALSE: {
                    pc = NO_JUMP;  /* always false; do nothing */
                    break;
                }
                default: {
                    pc = lcode.jumponcond (fs, e, 1);
                    break;
                }
            }
            luaK_concat (fs, ref e.t, pc);  /* insert last jump in 't' list */
            luaK_patchtohere (fs, e.f);
            e.f = NO_JUMP;
        }


        public static void luaK_indexed (FuncState fs, expdesc t, expdesc k) {
            lua_assert (lcode.hasjumps (t) == false);
            t.u.ind.t = (byte)t.u.info;
            t.u.ind.idx = (short)luaK_exp2RK (fs, k);
            t.u.ind.vt = (t.k == expkind.VUPVAL) ? expkind.VUPVAL
                                                 : check_exp<expkind> (vkisinreg (t.k), expkind.VLOCAL);
            t.k = expkind.VINDEXED;
        }


        public static void luaK_prefix (FuncState fs, UnOpr op, expdesc e, int line) {
            expdesc e2 = new expdesc ();
            e2.t = NO_JUMP;
            e2.f = NO_JUMP;
            e2.k = expkind.VKINT;
            e2.u.ival = 0;
            switch (op) {
                case UnOpr.OPR_MINUS: goto case UnOpr.OPR_LEN;
                case UnOpr.OPR_BNOT: goto case UnOpr.OPR_LEN;
                case UnOpr.OPR_LEN: {
                    lcode.codeexpval (fs, OpCode.OP_UNM + (int)(op - UnOpr.OPR_MINUS), e, e2, line);
                    break;
                }
                case UnOpr.OPR_NOT: {
                    lcode.codenot (fs, e);
                    break;
                }
                default: {
                    lua_assert (false);
                    break;
                }
            }
        }


        public static void luaK_infix (FuncState fs, BinOpr op, expdesc v) {
            switch (op) {
                case BinOpr.OPR_AND: {
                    luaK_goiftrue (fs, v);
                    break;
                }
                case BinOpr.OPR_OR: {
                    luaK_goiffalse (fs, v);
                    break;
                }
                case BinOpr.OPR_CONCAT: {
                    luaK_exp2nextreg (fs, v);  /* operand must be on the 'stack' */
                    break;
                }
                case BinOpr.OPR_ADD: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SUB: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_MUL: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_DIV: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_IDIV: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_MOD: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_POW: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BAND: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BOR: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BXOR: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SHL: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SHR: {
                    if (lcode.tonumeral (v, null) == false) luaK_exp2RK (fs, v);
                    break;
                }
                default: {
                    luaK_exp2RK (fs, v);
                    break;
                }
            }
        }


        public static void luaK_posfix (FuncState fs, BinOpr op, expdesc e1, expdesc e2, int line) {
            switch (op) {
                case BinOpr.OPR_AND: {
                    lua_assert (e1.t == NO_JUMP);  /* list must be closed */
                    luaK_dischargevars (fs, e2);
                    luaK_concat (fs, ref e2.f, e1.f);
                    e1.copy (e2);
                    break;
                }
                case BinOpr.OPR_OR: {
                    lua_assert (e1.f == NO_JUMP);  /* list must be closed */
                    luaK_dischargevars (fs, e2);
                    luaK_concat (fs, ref e2.t, e1.t);
                    e1.copy (e2);
                    break;
                }
                case BinOpr.OPR_CONCAT: {
                    luaK_exp2val (fs, e2);
                    if (e2.k == expkind.VRELOCABLE && GET_OPCODE (fs, e2.u.info) == OpCode.OP_CONCAT) {
                        lua_assert (e1.u.info == GETARG_B (fs, e2.u.info) - 1);
                        lcode.freeexp (fs, e1);
                        SETARG_B (fs, e2.u.info, e1.u.info);
                        e1.k = expkind.VRELOCABLE;
                        e1.u.info = e2.u.info;
                    }
                    else {
                        luaK_exp2nextreg (fs, e2);  /* operand must be on the 'stack' */
                        lcode.codeexpval (fs, OpCode.OP_CONCAT, e1, e2, line);
                    }
                    break;
                }
                case BinOpr.OPR_ADD: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SUB: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_MUL: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_DIV: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_IDIV: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_MOD: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_POW: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BAND: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BOR: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_BXOR: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SHL: goto case BinOpr.OPR_SHR;
                case BinOpr.OPR_SHR: {
                    lcode.codeexpval (fs, OpCode.OP_ADD + (int)(op - BinOpr.OPR_ADD), e1, e2, line);
                    break;
                }
                case BinOpr.OPR_EQ: goto case BinOpr.OPR_LE;
                case BinOpr.OPR_LT: goto case BinOpr.OPR_LE;
                case BinOpr.OPR_LE: {
                    lcode.codecomp (fs, OpCode.OP_EQ + (int)(op - BinOpr.OPR_EQ), 1, e1, e2);
                    break;
                }
                case BinOpr.OPR_NE: goto case BinOpr.OPR_GE;
                case BinOpr.OPR_GT: goto case BinOpr.OPR_GE;
                case BinOpr.OPR_GE: {
                    lcode.codecomp (fs, OpCode.OP_EQ + (int)(op - BinOpr.OPR_NE), 0, e1, e2);
                    break;
                }
                default: {
                    lua_assert (false);
                    break;
                }
            }
        }


        public static void luaK_fixline (FuncState fs, int line) {
            fs.f.lineinfo[fs.pc - 1] = line;
        }


        public static void luaK_setlist (FuncState fs, int nbase, int nelems, int tostore) {
            int c = (nelems - 1) / LFIELDS_PER_FLUSH + 1;
            int b = (tostore == lua530.LUA_MULTRET) ? 0 : tostore;
            lua_assert (tostore != 0);
            if (c <= MAXARG_C)
                luaK_codeABC (fs, OpCode.OP_SETLIST, nbase, b, c);
            else if (c <= MAXARG_Ax) {
                luaK_codeABC (fs, OpCode.OP_SETLIST, nbase, b, 0);
                lcode.codeextraarg (fs, c);
            }
            else
                luaX_syntaxerror (fs.ls, "constructor too long");
            fs.freereg = (byte)(nbase + 1);  /* free registers with list values */
        }
    }
}
