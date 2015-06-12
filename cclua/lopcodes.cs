using System;

namespace cclua {

    public static partial class imp {

        /*===========================================================================
          We assume that instructions are unsigned numbers.
          All instructions have an opcode in the first 6 bits.
          Instructions can have the following fields:
	        'A' : 8 bits
	        'B' : 9 bits
	        'C' : 9 bits
	        'Ax' : 26 bits ('A', 'B', and 'C' together)
	        'Bx' : 18 bits ('B' and 'C' together)
	        'sBx' : signed Bx

          A signed argument is represented in excess K; that is, the number
          value is the unsigned value minus K. K is exactly the maximum value
          for that argument (so that -max is represented by 0, and +max is
          represented by 2*max), which is half the maximum for the corresponding
          unsigned argument.
        ===========================================================================*/


        public enum OpMode { iABC, iABx, iAsBx, iAx }  /* basic instruction format */


        /*
        ** size and position of opcode arguments.
        */
        public const int SIZE_C = 9;
        public const int SIZE_B = 9;
        public const int SIZE_Bx = (SIZE_C + SIZE_B);
        public const int SIZE_A = 8;
        public const int SIZE_Ax = (SIZE_C + SIZE_B + SIZE_A);

        public const int SIZE_OP = 6;

        public const int POS_OP = 0;
        public const int POS_A = (POS_OP + SIZE_OP);
        public const int POS_C = (POS_A + SIZE_A);
        public const int POS_B = (POS_C + SIZE_C);
        public const int POS_Bx = POS_C;
        public const int POS_Ax = POS_A;


        /*
        ** limits for opcode arguments.
        ** we use (signed) int to manipulate most arguments,
        ** so they must fit in LUAI_BITSINT-1 bits (-1 for sign)
        */
        public const int MAXARG_Bx = MAX_INT;
        public const int MAXARG_sBx = MAX_INT;

        public const int MAXARG_Ax = MAX_INT;


        public const int MAXARG_A = ((1 << SIZE_A) - 1);
        public const int MAXARG_B = ((1 << SIZE_B) - 1);
        public const int MAXARG_C = ((1 << SIZE_C) - 1);


        /* creates a mask with 'n' 1 bits at position 'p' */
        public static uint MASK1 (int n, int p) { return ((~((~(uint)0) << n)) << p); }

        /* creates a mask with 'n' 0 bits at position 'p' */
        public static uint MASK0 (int n, int p) { return (~MASK1 (n, p)); }

        /*
        ** the following macros help to manipulate instructions
        */

        public static OpCode GET_OPCODE (uint i) { return (OpCode)((i >> POS_OP) & MASK1 (SIZE_OP, 0)); }
        public static OpCode GET_OPCODE (FuncState fs, int i) { return GET_OPCODE (fs.f.code[i]); }
        public static void SET_OPCODE (ref uint i, int o) { i = (uint)((i & MASK0 (SIZE_OP, POS_OP)) | ((o << POS_OP) & MASK1 (SIZE_OP, POS_OP))); }
        public static void SET_OPCODE (FuncState fs, int i, int o) { SET_OPCODE (ref fs.f.code[i], o); }

        public static int getarg (uint i, int pos, int size) { return (int)((i >> pos) & MASK1 (size, 0)); }
        public static void setarg (ref uint i, int v, int pos, int size) { i = ((i & MASK0 (size, pos)) | (((uint)(v) << pos) & MASK1 (size, pos))); }

        public static int GETARG_A (uint i) { return getarg (i, POS_A, SIZE_A); }
        public static int GETARG_A (FuncState fs, int i) { return GETARG_A (fs.f.code[i]); }
        public static void SETARG_A (ref uint i, int v) { setarg (ref i, v, POS_A, SIZE_A); }
        public static void SETARG_A (FuncState fs, int i, int v) { SETARG_A (ref fs.f.code[i], v); }

        public static int GETARG_B (uint i) { return getarg (i, POS_B, SIZE_B); }
        public static int GETARG_B (FuncState fs, int i) { return GETARG_B (fs.f.code[i]); }
        public static void SETARG_B (ref uint i, int v) { setarg (ref i, v, POS_B, SIZE_B); }
        public static void SETARG_B (FuncState fs, int i, int v) { SETARG_B (ref fs.f.code[i], v); }

        public static int GETARG_C (uint i) { return getarg (i, POS_C, SIZE_C); }
        public static int GETARG_C (FuncState fs, int i) { return GETARG_C (fs.f.code[i]); }
        public static void SETARG_C (ref uint i, int v) { setarg (ref i, v, POS_C, SIZE_C); }
        public static void SETARG_C (FuncState fs, int i, int v) { SETARG_C (ref fs.f.code[i], v); }

        public static int GETARG_Bx (uint i) { return getarg (i, POS_Bx, SIZE_Bx); }
        public static int GETARG_Bx (FuncState fs, int i) { return GETARG_Bx (fs.f.code[i]); }
        public static void SETARG_Bx (ref uint i, int v) { setarg (ref i, v, POS_Bx, SIZE_Bx); }
        public static void SETARG_Bx (FuncState fs, int i, int v) { SETARG_Bx (ref fs.f.code[i], v); }

        public static int GETARG_Ax (uint i) { return getarg (i, POS_Ax, SIZE_Ax); }
        public static int GETARG_Ax (FuncState fs, int i) { return GETARG_Ax (fs.f.code[i]); }
        public static void SETARG_Ax (ref uint i, int v) { setarg (ref i, v, POS_Ax, SIZE_Ax); }
        public static void SETARG_Ax (FuncState fs, int i, int v) { SETARG_Ax (ref fs.f.code[i], v); }

        public static int GETARG_sBx (uint i) { return (GETARG_Bx (i) - MAXARG_sBx); }
        public static int GETARG_sBx (FuncState fs, int i) { return GETARG_sBx (fs.f.code[i]); }
        public static void SETARG_sBx (ref uint i, int b) { SETARG_Bx (ref i, b + MAXARG_sBx); }
        public static void SETARG_sBx (FuncState fs, int i, int v) { SETARG_sBx (ref fs.f.code[i], v); }


        public static uint CREATE_ABC (OpCode o, int a, int b, int c) { return ((((uint)o) << POS_OP) | (((uint)a) << POS_A) | (((uint)b) << POS_B) | (((uint)c) << POS_C)); }

        public static uint CREATE_ABx (OpCode o, int a, int bc) { return ((((uint)o) << POS_OP) | (((uint)a) << POS_A) | (((uint)bc) << POS_Bx)); }

        public static uint CREATE_Ax (OpCode o, int a) { return ((((uint)o) << POS_OP) | (((uint)a) << POS_Ax)); }


        /*
        ** Macros to operate RK indices
        */

        /* this bit 1 means constant (0 means register) */
        public const int BITRK = (1 << (SIZE_B - 1));

        /* test whether value is a constant */
        public static bool ISK (int x) { return ((x & BITRK) != 0); }

        /* gets the index of the constant */
        public static int INDEXK (int r) { return (r & (~BITRK)); }

        public const int MAXINDEXRK = BITRK - 1;

        /* code a constant index as a RK value */
        public static int RKASK (int x) { return (x | BITRK); }



        /*
        ** invalid register that fits in 8 bits
        */
        public const int NO_REG = MAXARG_A;


        /*
        ** R(x) - register
        ** Kst(x) - constant (in constant table)
        ** RK(x) == if ISK(x) then Kst(INDEXK(x)) else R(x)
        */



        /*
        ** grep "ORDER OP" if you change these enums
        */

        public enum OpCode {
            /*----------------------------------------------------------------------
            name		args	description
            ------------------------------------------------------------------------*/
            OP_MOVE,/*	A B	R(A) := R(B)					*/
            OP_LOADK,/*	A Bx	R(A) := Kst(Bx)					*/
            OP_LOADKX,/*	A 	R(A) := Kst(extra arg)				*/
            OP_LOADBOOL,/*	A B C	R(A) := (Bool)B; if (C) pc++			*/
            OP_LOADNIL,/*	A B	R(A), R(A+1), ..., R(A+B) := nil		*/
            OP_GETUPVAL,/*	A B	R(A) := UpValue[B]				*/

            OP_GETTABUP,/*	A B C	R(A) := UpValue[B][RK(C)]			*/
            OP_GETTABLE,/*	A B C	R(A) := R(B)[RK(C)]				*/

            OP_SETTABUP,/*	A B C	UpValue[A][RK(B)] := RK(C)			*/
            OP_SETUPVAL,/*	A B	UpValue[B] := R(A)				*/
            OP_SETTABLE,/*	A B C	R(A)[RK(B)] := RK(C)				*/

            OP_NEWTABLE,/*	A B C	R(A) := {} (size = B,C)				*/

            OP_SELF,/*	A B C	R(A+1) := R(B); R(A) := R(B)[RK(C)]		*/

            OP_ADD,/*	A B C	R(A) := RK(B) + RK(C)				*/
            OP_SUB,/*	A B C	R(A) := RK(B) - RK(C)				*/
            OP_MUL,/*	A B C	R(A) := RK(B) * RK(C)				*/
            OP_MOD,/*	A B C	R(A) := RK(B) % RK(C)				*/
            OP_POW,/*	A B C	R(A) := RK(B) ^ RK(C)				*/
            OP_DIV,/*	A B C	R(A) := RK(B) / RK(C)				*/
            OP_IDIV,/*	A B C	R(A) := RK(B) // RK(C)				*/
            OP_BAND,/*	A B C	R(A) := RK(B) & RK(C)				*/
            OP_BOR,/*	A B C	R(A) := RK(B) | RK(C)				*/
            OP_BXOR,/*	A B C	R(A) := RK(B) ~ RK(C)				*/
            OP_SHL,/*	A B C	R(A) := RK(B) << RK(C)				*/
            OP_SHR,/*	A B C	R(A) := RK(B) >> RK(C)				*/
            OP_UNM,/*	A B	R(A) := -R(B)					*/
            OP_BNOT,/*	A B	R(A) := ~R(B)					*/
            OP_NOT,/*	A B	R(A) := not R(B)				*/
            OP_LEN,/*	A B	R(A) := length of R(B)				*/

            OP_CONCAT,/*	A B C	R(A) := R(B).. ... ..R(C)			*/

            OP_JMP,/*	A sBx	pc+=sBx; if (A) close all upvalues >= R(A - 1)	*/
            OP_EQ,/*	A B C	if ((RK(B) == RK(C)) ~= A) then pc++		*/
            OP_LT,/*	A B C	if ((RK(B) <  RK(C)) ~= A) then pc++		*/
            OP_LE,/*	A B C	if ((RK(B) <= RK(C)) ~= A) then pc++		*/

            OP_TEST,/*	A C	if not (R(A) <=> C) then pc++			*/
            OP_TESTSET,/*	A B C	if (R(B) <=> C) then R(A) := R(B) else pc++	*/

            OP_CALL,/*	A B C	R(A), ... ,R(A+C-2) := R(A)(R(A+1), ... ,R(A+B-1)) */
            OP_TAILCALL,/*	A B C	return R(A)(R(A+1), ... ,R(A+B-1))		*/
            OP_RETURN,/*	A B	return R(A), ... ,R(A+B-2)	(see note)	*/

            OP_FORLOOP,/*	A sBx	R(A)+=R(A+2);
			if R(A) <?= R(A+1) then { pc+=sBx; R(A+3)=R(A) }*/
            OP_FORPREP,/*	A sBx	R(A)-=R(A+2); pc+=sBx				*/

            OP_TFORCALL,/*	A C	R(A+3), ... ,R(A+2+C) := R(A)(R(A+1), R(A+2));	*/
            OP_TFORLOOP,/*	A sBx	if R(A+1) ~= nil then { R(A)=R(A+1); pc += sBx }*/

            OP_SETLIST,/*	A B C	R(A)[(C-1)*FPF+i] := R(A+i), 1 <= i <= B	*/

            OP_CLOSURE,/*	A Bx	R(A) := closure(KPROTO[Bx])			*/

            OP_VARARG,/*	A B	R(A), R(A+1), ..., R(A+B-2) = vararg		*/

            OP_EXTRAARG/*	Ax	extra (larger) argument for previous opcode	*/
        }

        public const int NUM_OPCODES = ((int)OpCode.OP_EXTRAARG + 1);


        /*===========================================================================
          Notes:
          (*) In OP_CALL, if (B == 0) then B = top. If (C == 0), then 'top' is
          set to last_result+1, so next open instruction (OP_CALL, OP_RETURN,
          OP_SETLIST) may use 'top'.

          (*) In OP_VARARG, if (B == 0) then use actual number of varargs and
          set top (like in OP_CALL with C == 0).

          (*) In OP_RETURN, if (B == 0) then return up to 'top'.

          (*) In OP_SETLIST, if (B == 0) then B = 'top'; if (C == 0) then next
          'instruction' is EXTRAARG(real C).

          (*) In OP_LOADKX, the next 'instruction' is always EXTRAARG.

          (*) For comparisons, A specifies what condition the test should accept
          (true or false).

          (*) All 'skips' (pc++) assume that next instruction is a jump.

        ===========================================================================*/


        /*
        ** masks for instruction properties. The format is:
        ** bits 0-1: op mode
        ** bits 2-3: C arg mode
        ** bits 4-5: B arg mode
        ** bit 6: instruction set register A
        ** bit 7: operator is a test (next instruction must be a jump)
        */

        public enum OpArgMask {
            OpArgN,  /* argument is not used */
            OpArgU,  /* argument is used */
            OpArgR,  /* argument is a register or a jump offset */
            OpArgK   /* argument is a constant or register/constant */
        }

        public static OpMode getOpMode (OpCode m) { return (OpMode)(luaP_opmodes[(int)m] & 3); }
        public static OpArgMask getBMode (OpCode m) { return (OpArgMask)((luaP_opmodes[(int)m] >> 4) & 3); }
        public static OpArgMask getCMode (OpCode m) { return (OpArgMask)((luaP_opmodes[(int)m] >> 2) & 3); }
        public static bool testAMode (OpCode m) { return ((luaP_opmodes[(int)m] & (1 << 6)) != 0); }
        public static bool testTMode (OpCode m) { return ((luaP_opmodes[(int)m] & (1 << 7)) != 0); }


        /* number of list items to accumulate before a SETLIST instruction */
        public const int LFIELDS_PER_FLUSH = 50;


        /* ORDER OP */

        public static string[] luaP_opnames = {
              "MOVE",
              "LOADK",
              "LOADKX",
              "LOADBOOL",
              "LOADNIL",
              "GETUPVAL",
              "GETTABUP",
              "GETTABLE",
              "SETTABUP",
              "SETUPVAL",
              "SETTABLE",
              "NEWTABLE",
              "SELF",
              "ADD",
              "SUB",
              "MUL",
              "MOD",
              "POW",
              "DIV",
              "IDIV",
              "BAND",
              "BOR",
              "BXOR",
              "SHL",
              "SHR",
              "UNM",
              "BNOT",
              "NOT",
              "LEN",
              "CONCAT",
              "JMP",
              "EQ",
              "LT",
              "LE",
              "TEST",
              "TESTSET",
              "CALL",
              "TAILCALL",
              "RETURN",
              "FORLOOP",
              "FORPREP",
              "TFORCALL",
              "TFORLOOP",
              "SETLIST",
              "CLOSURE",
              "VARARG",
              "EXTRAARG",
              null
        };


        public static byte opmode (int t, int a, OpArgMask b, OpArgMask c, OpMode m) { return (byte)((t << 7) | (a << 6) | ((int)b << 4) | ((int)c << 2) | ((int)m)); }

        public static byte[] luaP_opmodes = {
            /*       T  A    B               C                 mode		    opcode	*/
              opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iABC)		/* OP_MOVE */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgN, OpMode.iABx)		/* OP_LOADK */
             ,opmode(0, 1, OpArgMask.OpArgN, OpArgMask.OpArgN, OpMode.iABx)		/* OP_LOADKX */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iABC)		/* OP_LOADBOOL */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABC)		/* OP_LOADNIL */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABC)		/* OP_GETUPVAL */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgK, OpMode.iABC)		/* OP_GETTABUP */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgK, OpMode.iABC)		/* OP_GETTABLE */
             ,opmode(0, 0, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SETTABUP */
             ,opmode(0, 0, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABC)		/* OP_SETUPVAL */
             ,opmode(0, 0, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SETTABLE */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iABC)		/* OP_NEWTABLE */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SELF */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_ADD */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SUB */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_MUL */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_MOD */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_POW */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_DIV */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_IDIV */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_BAND */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_BOR */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_BXOR */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SHL */
             ,opmode(0, 1, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_SHR */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iABC)		/* OP_UNM */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iABC)		/* OP_BNOT */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iABC)		/* OP_NOT */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iABC)		/* OP_LEN */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgR, OpMode.iABC)		/* OP_CONCAT */
             ,opmode(0, 0, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iAsBx)		/* OP_JMP */
             ,opmode(1, 0, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_EQ */
             ,opmode(1, 0, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_LT */
             ,opmode(1, 0, OpArgMask.OpArgK, OpArgMask.OpArgK, OpMode.iABC)		/* OP_LE */
             ,opmode(1, 0, OpArgMask.OpArgN, OpArgMask.OpArgU, OpMode.iABC)		/* OP_TEST */
             ,opmode(1, 1, OpArgMask.OpArgR, OpArgMask.OpArgU, OpMode.iABC)		/* OP_TESTSET */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iABC)		/* OP_CALL */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iABC)		/* OP_TAILCALL */
             ,opmode(0, 0, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABC)		/* OP_RETURN */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iAsBx)		/* OP_FORLOOP */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iAsBx)		/* OP_FORPREP */
             ,opmode(0, 0, OpArgMask.OpArgN, OpArgMask.OpArgU, OpMode.iABC)		/* OP_TFORCALL */
             ,opmode(0, 1, OpArgMask.OpArgR, OpArgMask.OpArgN, OpMode.iAsBx)		/* OP_TFORLOOP */
             ,opmode(0, 0, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iABC)		/* OP_SETLIST */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABx)		/* OP_CLOSURE */
             ,opmode(0, 1, OpArgMask.OpArgU, OpArgMask.OpArgN, OpMode.iABC)		/* OP_VARARG */
             ,opmode(0, 0, OpArgMask.OpArgU, OpArgMask.OpArgU, OpMode.iAx)		/* OP_EXTRAARG */
        };
    }
}
