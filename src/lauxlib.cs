using System;
using System.IO;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using lua_Debug = cclua.lua530.lua_Debug;

namespace cclua {
	
	public static partial class imp {

        public static class laux {

            public static bool feof (FileStream f) {
                return true;
            }

            public static int fread (byte[] buff, int block, FileStream f) { return 0; }


            public static string bom = "\xEF\xBB\xBF";  /* Utf8 BOM mark */

            public static byte getc (FileStream f) { return 0; }
        }

        public const byte EOF = (byte)0xff;
        public static FileStream stdin = null;
        public static FileStream fopen (string fn, string mode) { return null; }
        public static FileStream freopen (string fn, string mode, FileStream f) { return f; }
        public static int ferror (FileStream f) { return 0; }
        public static void fclose (FileStream f) { }


		/*
		** {======================================================
		** Traceback
		** =======================================================
		*/



		public const int LEVELS1 = 12;  /* size of the first part of the stack */
		public const int LEVELS2 = 10;  /* size of the second part of the stack */



		/*
		** search for 'objidx' in table at index -1.
		** return 1 + string at top if find a good name.
		*/
		public static bool findfield (lua_State L, int objidx, int level) {
			if (level == 0 || cc.lua_istable (L, -1) == 0)
				return false;  /* not found */
			cc.lua_pushnil (L);  /* start 'next' loop */
			while (cc.lua_next (L, -2) != 0) {  /* for each pair in table */
				if (cc.lua_type (L, -2) == cc.LUA_TSTRING) {  /* ignore non-string keys */
					if (cc.lua_rawequal (L, objidx, -1) != 0) {  /* found object? */
						cc.lua_pop (L, 1);  /* remove value (but keep name) */
						return true;
					}
					else if (findfield (L, objidx, level - 1)) {  /* try recursively */
						cc.lua_remove (L, -2);  /* remove table (but keep name) */
						cc.lua_pushliteral (L, ".");
						cc.lua_insert (L, -2);  /* place '.' between the two names */
						cc.lua_concat (L, 3);
						return true;
					}
				}
				cc.lua_pop (L, 1);  /* remove value */
			}
			return false;  /* not found */
		}


		/*
		** Search for a name for a function in all loaded modules
		** (registry._LOADED).
		*/
		public static bool pushglobalfuncname (lua_State L, lua_Debug ar) {
			int top = cc.lua_gettop (L);
            cc.lua_getinfo (L, "f", ar);  /* push function */
			cc.lua_getfield (L, cc.LUA_REGISTRYINDEX, "_LOADED");
			if (findfield (L, top + 1, 2)) {
				string name = cc.lua_tostring (L, -1);
				if (name.Substring (0, 3) == "_G.") {  /* name start with '_G.'? */
					cc.lua_pushstring (L, name.Substring (3));  /* push name without prefix */
					cc.lua_remove (L, -2);  /* remove original name */
				}
				cc.lua_copy (L, -1, top + 1);  /* move name to proper place */
				cc.lua_pop (L, 2);  /* remove pushed values */
				return true;
			}
			else {
				cc.lua_settop (L, top);  /* remove function and global table */
				return false;
			}
		}


		public static void pushfuncname (lua_State L, lua_Debug ar) {
			if (pushglobalfuncname (L, ar)) {  /* try first a global name */
                cc.lua_pushfstring (L, "function '%s'", cc.lua_tostring (L, -1));
				cc.lua_remove (L, -2);  /* remove name */
			}
			else if (ar.namewhat[0] != '\0')  /* is there a name from code? */
				cc.lua_pushfstring (L, "%s %s", ar.namewhat, ar.name);  /* use it */
            else if (ar.what[0] == 'm')  /* main? */
				cc.lua_pushliteral (L, "main chunk");
            else if (ar.what[0] != 'C')  /* for Lua functions, use <file:line> */
				cc.lua_pushfstring (L, "function <%s:%d>", ar.short_src, ar.linedefined);
			else  /* nothing left... */
				cc.lua_pushliteral (L, "?");
		}


		public static int countlevels (lua_State L) {
			lua_Debug ar = new lua_Debug ();
			int li = 1;
			int le = 1;
			while (cc.lua_getstack (L, le, ar) != 0) { li = le; le *= 2; }
			while (li < le) {
				int m = (li + le) / 2;
				if (cc.lua_getstack (L, m, ar) != 0) li = m + 1;
				else le = m;
			}
			return le - 1;
		}


        public static int typeerror (lua_State L, int arg, string tname) {
            string typearg = null;
            if (cc.luaL_getmetafield (L, arg, "__name") == cc.LUA_TSTRING)
                typearg = cc.lua_tostring (L, -1);
            else if (cc.lua_type (L, arg) == cc.LUA_TLIGHTUSERDATA)
                typearg = "light userdata";
            else
                typearg = cc.luaL_typename (L, arg);
            string msg = cc.lua_pushfstring (L, "%s expected, got %s", tname, typearg);
            return cc.luaL_argerror (L, arg, msg);
        }


        public static void tag_error (lua_State L, int arg, int tag) {
            typeerror (L, arg, cc.lua_typename (L, tag));
        }


        public static string strerror (int en) { return ""; }


        public static void l_inspectstat (int stat, string what) { }  /* no op */


        public static void interror (lua_State L, int arg) {
            if (cc.lua_isnumber (L, arg) != 0)
                cc.luaL_argerror (L, arg, "number has no integer representation");
            else
                tag_error (L, arg, cc.LUA_TNUMBER);
        }


        /* index of free-list header */
        public const int freelist = 0;


        public class LoadF {
            public int n;  /* number of pre-read characters */
            public FileStream f;  /* file being read */
            public byte[] buff;  /* area for reading file */

            public LoadF () {
                buff = new byte[4096];
            }
        }


        public static byte[] getF (lua_State L, object ud, ref int size) {
            LoadF lf = (LoadF)ud;
            if (lf.n > 0) {  /* are there pre-read characters to be read? */
                size = lf.n;  /* return them (chars already in buffer) */
                lf.n = 0;  /* no more pre-read characters */
            }
            else {  /* read a block from file */
                /* 'fread' can return > 0 *and* set the EOF flag. If next call to
                   'getF' called 'fread', it might still wait for user input.
                   The next check avoids this problem. */
                if (laux.feof (lf.f)) return null;
                size = laux.fread (lf.buff, 1, lf.f);  /* read block */
            }
            return lf.buff;
        }


        public static int errfile (lua_State L, string what, int fnameindex) {
            // TODO: errno
            string serr = imp.strerror (0);
            string filename = cc.lua_tostring (L, fnameindex).Substring (1);
            cc.lua_pushfstring (L, "cannot %s %s: %s", what, filename, serr);
            cc.lua_remove (L, fnameindex);
            return cc.LUA_ERRFILE;
        }


        public static int skipBOM (LoadF lf) {
            byte c = 0;
            lf.n = 0;
            int i = 0;
            do {
                c = laux.getc (lf.f);
                if (c == EOF || c != laux.bom[i++]) return c;
                lf.buff[lf.n++] = c;  /* to be read by the parser */
            } while (i < laux.bom.Length);
            lf.n = 0;  /* prefix matched; discard it */
            return laux.getc (lf.f);  /* return next character */
        }


        /*
        ** reads the first character of file 'f' and skips an optional BOM mark
        ** in its beginning plus its first line if it starts with '#'. Returns
        ** true if it skipped the first line.  In any case, '*cp' has the
        ** first "valid" character of the file (after the optional BOM and
        ** a first-line comment).
        */
        public static bool skipcomment (LoadF lf, ref int cp) {
            int c = skipBOM (lf);
            cp = c;
            if (c == '#') {  /* first line is a comment (Unix exec. file)? */
                do {  /* skip first line */
                    c = laux.getc (lf.f);
                } while (c != EOF && c != '\n');
                cp = laux.getc (lf.f);  /* skip end-of-line, if present */
                return true;  /* there was a comment */
            }
            else return false;  /* no comment */
        }


        public class LoadS {
            public byte[] s;
            public int size;
        }


        public static byte[] getS (lua_State L, object ud, ref int size) {
            LoadS ls = (LoadS)ud;
            if (ls.size == 0) return null;
            size = ls.size;
            ls.size = 0;
            return ls.s;
        }


        public class l_alloc : cc.lua_Alloc {
        }

		
		public static int panic (lua_State L) {
            cc.lua_writestringerror ("PANIC: unprotected error in call to Lua API ({0})\n", cc.lua_tostring (L, -1));
			return 0; /* return to Lua to abort */
		}
	}

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

		/* extra error code for 'luaL_load' */
		public const int LUA_ERRFILE = LUA_ERRERR + 1;


		public class luaL_Reg {
			public string name;
			public lua_CFunction func;

			public luaL_Reg (string n, lua_CFunction f) { name = n; func = f; }
		}


		public const int LUAL_NUMSIZES = (sizeof (long) * 16 + sizeof (double));


		public static void luaL_checkversion (lua_State L) { luaL_checkversion_ (L, LUA_VERSION_NUM, LUAL_NUMSIZES); }


		/* pre-defined references */
		public const int LUA_NOREF = -2;
		public const int LUA_REFNIL = -1;


		public static int luaL_loadfile (lua_State L, string f) { return luaL_loadfilex (L, f, null); }


		/*
		** ===============================================================
		** some useful macros
		** ===============================================================
		*/


		public static void luaL_newlibtable (lua_State L, luaL_Reg[] l) { lua_createtable (L, 0, l.Length); }

		public static void luaL_newlib (lua_State L, luaL_Reg[] l) { luaL_checkversion (L); luaL_newlibtable (L, l); luaL_setfuncs (L, l, 0); }

		public static void luaL_argcheck (lua_State L, bool cond, int arg, string extramsg) { if (cond == false) luaL_argerror (L, arg, extramsg); }
        public static string luaL_checkstring (lua_State L, int n) { int i = 0; return luaL_checklstring (L, n, ref i); }
        public static string luaL_optstring (lua_State L, int n, string d) { int i = 0; return luaL_optlstring (L, n, d, ref i); }

		public static string luaL_typename (lua_State L, int i) { return lua_typename (L, lua_type (L, i)); }

        public static void luaL_dofile (lua_State L, string fn) { luaL_loadfile (L, fn); lua_pcall (L, 0, LUA_MULTRET, 0); }

		public static void luaL_dostring (lua_State L, string s) { if (luaL_loadstring (L, s) == 0) lua_pcall (L, 0, LUA_MULTRET, 0); }

		public static int luaL_getmetatable (lua_State L, string n) { return lua_getfield (L, LUA_REGISTRYINDEX, n); }

        public static int luaL_loadbuffer (lua_State L, byte[] s, int sz, string n) { return luaL_loadbufferx (L, s, sz, n, null); }


		/*
		** {======================================================
		** Generic Buffer manipulation
		** =======================================================
		*/





		public static void luaL_traceback (lua_State L, lua_State L1, string msg, int level) {
            lua_Debug ar = new lua_Debug (); ;
			int top = lua_gettop (L);
			int numlevels = imp.countlevels (L1);
			int mark = (numlevels > imp.LEVELS1 + imp.LEVELS2) ? imp.LEVELS1 : 0;
			if (msg != null) lua_pushfstring (L, "%s\n", msg);
			lua_pushliteral (L, "stack traceback:");
            while (lua_getstack (L1, level++, ar) != 0) {
                if (level == mark) {
                    lua_pushliteral (L, "\n\t...");
                    level = numlevels - imp.LEVELS2;
                }
                else {
                    lua_getinfo (L1, "Slnt", ar);
                    lua_pushfstring (L, "\n\t%s:", ar.short_src);
                    if (ar.currentline > 0)
                        lua_pushfstring (L, "%d:", ar.currentline);
                    lua_pushliteral (L, " in ");
                    imp.pushfuncname (L, ar);
                    if (ar.istailcall != 0)
                        lua_pushliteral (L, "\n\t(...tail calls...)");
                    lua_concat (L, lua_gettop (L) - top);
                }
            }
            lua_concat (L, lua_gettop (L) - top);
		}

        /* }====================================================== */


        /*
        ** {======================================================
        ** Error-report functions
        ** =======================================================
        */

        public static int luaL_argerror (lua_State L, int arg, string extramsg) {
            lua_Debug ar = new lua_Debug ();
            if (lua_getstack (L, 0, ar) == 0)
                return luaL_error (L, "bad argument #%d (%s)", arg, extramsg);
            lua_getinfo (L, "n", ar);
            if (ar.namewhat == "method") {
                arg--;
                if (arg == 0)
                    return luaL_error (L, "calling '%s' on bad self (%s)", ar.name, extramsg);
            }
            if (ar.name == null)
                ar.name = (imp.pushglobalfuncname (L, ar) ? lua_tostring (L, -1) : "?");
            return luaL_error (L, "bad argument #%d to '%s' (%s)", arg, ar.name, extramsg);
        }


        public static void luaL_where (lua_State L, int level) {
            lua_Debug ar = new lua_Debug ();
            if (lua_getstack (L, level, ar) != 0) {  /* check function at level */
                lua_getinfo (L, "Sl", ar);  /* get info about it */
                if (ar.currentline > 0) {  /* is there info? */
                    lua_pushfstring (L, "%s:%d: ", ar.short_src, ar.currentline);
                    return;
                }
            }
            lua_pushliteral (L, "");  /* else, no information available... */
        }


        public static int luaL_error (lua_State L, string fmt, params object[] args) {
            // TODO
            return lua_error (L);
        }


        public static int luaL_fileresult (lua_State L, int stat, string fname) {
            // TODO : errno/strerror
            int en = 0;
            if (stat != 0) {
                lua_pushboolean (L, 1);
                return 1;
            }
            else {
                lua_pushnil (L);
                if (fname != null)
                    lua_pushfstring (L, "%s: %s", fname, imp.strerror (en));
                else
                    lua_pushfstring (L, imp.strerror (en));
                lua_pushinteger (L, en);
                return 3;
            }
        }


        public static int luaL_execresult (lua_State L, int stat) {
            string what = "exit";  /* type of termination */
            if (stat == -1)  /* error? */
                return luaL_fileresult (L, 0, null);
            else {
                imp.l_inspectstat (stat, what);  /* interpret result */
                if (what[0] == 'e' && stat == 0)  /* successful termination? */
                    lua_pushboolean (L, 1);
                else
                    lua_pushnil (L);
                lua_pushstring (L, what);
                lua_pushinteger (L, stat);
                return 3;  /* return true/nil,what,code */
            }
        }

        /* }====================================================== */


        /*
        ** {======================================================
        ** Userdata's metatable manipulation
        ** =======================================================
        */

        public static int luaL_newmetatable (lua_State L, string tname) {
            if (luaL_getmetatable (L, tname) != 0)  /* name already in use? */
                return 0;  /* leave previous value on top, but return 0 */
            lua_pop (L, 1);
            lua_newtable (L);  /* create metatable */
            lua_pushstring (L, tname);
            lua_setfield (L, -2, "__name");  /* metatable.__name = tname */
            lua_pushvalue (L, -1);
            lua_setfield (L, LUA_REGISTRYINDEX, tname);  /* registry.name = metatable */
            return 1;
        }


        public static void luaL_setmetatable (lua_State L, string tname) {
            luaL_getmetatable (L, tname);
            lua_setmetatable (L, -2);
        }


        public static object luaL_testudata (lua_State L, int ud, string tname) {
            object p = lua_touserdata (L, ud);
            if (p != null) {  /* value is a userdata? */
                if (lua_getmetatable (L, ud) != 0) {  /* does it have a metatable? */
                    luaL_getmetatable (L, tname);  /* get correct metatable */
                    if (lua_rawequal (L, -1, -2) == 0)  /* not the same? */
                        p = null;  /* value is a userdata with wrong metatable */
                    lua_pop (L, 2);  /* remove both metatables */
                    return p;
                }
            }
            return null;  /* value is not a userdata with a metatable */
        }


        public static object luaL_checkudata (lua_State L, int ud, string tname) {
            object p = luaL_testudata (L, ud, tname);
            if (p == null) imp.typeerror (L, ud, tname);
            return p;
        }

        /* }====================================================== */


        /*
        ** {======================================================
        ** Argument check functions
        ** =======================================================
        */

        public static int luaL_checkoption (lua_State L, int arg, string def, string[] lst) {
            string name = (def != null) ? luaL_optstring (L, arg, def) : luaL_checkstring (L, arg);
            int i = 0;
            for (; lst[i] != null; i++)
                if (lst[i] == name)
                    return i;
            return luaL_argerror (L, arg, lua_pushfstring (L, "invalid option '%s'", name));
        }


        public static void luaL_checkstack (lua_State L, int space, string msg) {
            /* keep some extra space to run error routines, if needed */
            int extra = LUA_MINSTACK;
            if (lua_checkstack (L, space + extra) == 0) {
                if (msg != null)
                    luaL_error (L, "stack overflow (%s)", msg);
                else
                    luaL_error (L, "stack overflow");
            }
        }


        public static void luaL_checktype (lua_State L, int arg, int t) {
            if (lua_type (L, arg) != t)
                imp.tag_error (L, arg, t);
        }


        public static void luaL_checkany (lua_State L, int arg) {
            if (lua_type (L, arg) == LUA_TNONE)
                luaL_argerror (L, arg, "value expected");
        }


        public static string luaL_checklstring (lua_State L, int arg, ref int len) {
            string s = lua_tolstring (L, arg, ref len);
            if (s == null) imp.tag_error (L, arg, LUA_TSTRING);
            return s;
        }


        public static string luaL_optlstring (lua_State L, int arg, string def, ref int len) {
            if (lua_isnoneornil (L, arg) != 0) {
                len = (def != null) ? def.Length : 0;
                return def;
            }
            else return luaL_checklstring (L, arg, ref len);
        }


        public static double luaL_checknumber (lua_State L, int arg) {
            int isnum = 0;
            double d = lua_tonumberx (L, arg, ref isnum);
            if (isnum == 0)
                imp.tag_error (L, arg, LUA_TNUMBER);
            return d;
        }


        public static double luaL_optnumber (lua_State L, int arg, double def) {
            return (lua_isnoneornil (L, arg) != 0 ? def : luaL_checknumber (L, arg));
        }


        public static long luaL_checkinteger (lua_State L, int arg) {
            int isnum = 0;
            long d = lua_tointegerx (L, arg, ref isnum);
            if (isnum == 0)
                imp.interror (L, arg);
            return d;
        }


        public static long luaL_optinteger (lua_State L, int arg, long def) {
            return (lua_isnoneornil (L, arg) != 0 ? def : luaL_checkinteger (L, arg));
        }

        /* }====================================================== */


        /*
        ** {======================================================
        ** Generic Buffer manipulation
        ** =======================================================
        */

        // TODO luaL_buffer

        /* }====================================================== */


        /*
        ** {======================================================
        ** Reference system
        ** =======================================================
        */
        public static int luaL_ref (lua_State L, int t) {
            if (lua_isnil (L, -1) != 0) {
                lua_pop (L, 1);  /* remove from stack */
                return LUA_REFNIL;  /* 'nil' has a unique fixed reference */
            }
            t = lua_absindex (L, t);
            lua_rawgeti (L, t, imp.freelist);  /* get first free element */
            int r = (int)lua_tointeger (L, -1);  /* ref = t[freelist] */
            lua_pop (L, 1);  /* remove it from stack */
            if (r != 0) {  /* any free element? */
                lua_rawgeti (L, t, r);  /* remove it from list */
                lua_rawseti (L, t, imp.freelist);  /* (t[freelist] = t[ref]) */
            }
            else  /* no free elements */
                r = (int)lua_rawlen (L, t) + 1;  /* get a new reference */
            lua_rawseti (L, t, r);
            return r;
        }


        public static void luaL_unref (lua_State L, int t, int r) {
            if (r >= 0) {
                t = lua_absindex (L, t);
                lua_rawgeti (L, t, imp.freelist);
                lua_rawseti (L, t, r);
                lua_pushinteger (L, r);
                lua_rawseti (L, t, imp.freelist);
            }
        }

        /* }====================================================== */


        /*
        ** {======================================================
        ** Load functions
        ** =======================================================
        */

        public static int luaL_loadfilex (lua_State L, string filename, string mode) {
            imp.LoadF lf = new imp.LoadF ();
            int fnameindex = lua_gettop (L) + 1;  /* index of filename on the stack */
            if (filename == null) {
                lua_pushliteral (L, "=stdin");
                lf.f = imp.stdin;
            }
            else {
                lua_pushfstring (L, "@%s", filename);
                lf.f = imp.fopen (filename, "r");
                if (lf.f == null) return imp.errfile (L, "open", fnameindex);
            }
            int c = 0;
            if (imp.skipcomment (lf, ref c))  /* read initial portion */
                lf.buff[lf.n++] = (byte)'\n';  /* add line to correct line numbers */
            if (c == LUA_SIGNATURE[0] && filename != null) {  /* binary file? */
                lf.f = imp.freopen (filename, "rb", lf.f);  /* reopen in binary mode */
                if (lf.f == null) return imp.errfile (L, "reopen", fnameindex);
                imp.skipcomment (lf, ref c);  /* re-read initial portion */
            }
            if (c != imp.EOF)
                lf.buff[lf.n++] = (byte)c;  /* 'c' is the first character of the stream */
            int status = lua_load (L, imp.getF, lf, lua_tostring (L, -1), mode);
            int readstatus = imp.ferror (lf.f);
            if (filename != null) imp.fclose (lf.f);  /* close file (even in case of errors) */
            if (readstatus != 0) {
                lua_settop (L, fnameindex);  /* ignore results from 'lua_load' */
                return imp.errfile (L, "read", fnameindex);
            }
            lua_remove (L, fnameindex);
            return status;
        }


        public static int luaL_loadbufferx (lua_State L, byte[] buff, int size, string name, string mode) {
            imp.LoadS ls = new imp.LoadS ();
            ls.s = buff;
            ls.size = size;
            return lua_load (L, imp.getS, ls, name, mode);
        }


        public static int luaL_loadstring (lua_State L, string s) {
            byte[] b = imp.str2byte (s);
            return luaL_loadbuffer (L, b, b.Length, s);
        }

        /* }====================================================== */



        public static int luaL_getmetafield (lua_State L, int obj, string ev) {
            if (lua_getmetatable (L, obj) == 0)
                return LUA_TNIL;
            else {
                lua_pushstring (L, ev);
                int tt = lua_rawget (L, -2);
                if (tt == LUA_TNIL)  /* is metafield nil? */
                    lua_pop (L, 2);  /* remove metatable and metafield */
                else
                    lua_remove (L, -2);  /* remove only metatable */
                return tt;  /* return metafield type */
            }
        }


        public static int luaL_callmeta (lua_State L, int obj, string ev) {
            obj = lua_absindex (L, obj);
            if (luaL_getmetafield (L, obj, ev) == LUA_TNIL)
                return 0;
            lua_pushvalue (L, obj);
            lua_call (L, 1, 1);
            return 1;
        }


        public static long luaL_len (lua_State L, int idx) {
            int isnum = 0;
            lua_len (L, idx);
            long l = lua_tointegerx (L, -1, ref isnum);
            if (isnum == 0)
                luaL_error (L, "object length is not an integer");
            lua_pop (L, 1);  /* remove object */
            return 1;
        }


        public static string luaL_tolstring (lua_State L, int idx, ref int len) {
            if (luaL_callmeta (L, LUA_RIDX_GLOBALS, "__tostring") == 0) {
                switch (lua_type (L, idx)) {
                    case LUA_TNUMBER: {
                        if (lua_isinteger (L, idx) != 0)
                            lua_pushfstring (L, "%I", lua_tointeger (L, idx));
                        else
                            lua_pushfstring (L, "%f", lua_tonumber (L, idx));
                        break;
                    }
                    case LUA_TSTRING: {
                        lua_pushvalue (L, idx);
                        break;
                    }
                    case LUA_TBOOLEAN: {
                        lua_pushstring (L, (lua_toboolean (L, idx) != 0 ? "true" : "false"));
                        break;
                    }
                    case LUA_TNIL: {
                        lua_pushliteral (L, "nil");
                        break;
                    }
                    default: {
                        lua_pushfstring (L, "%s: %p", luaL_typename (L, idx), lua_topointer (L, idx));
                        break;
                    }
                }
            }
            return lua_tolstring (L, -1, ref len);
        }



        /*
        ** set functions from list 'l' into table at top - 'nup'; each
        ** function gets the 'nup' elements at the top as upvalues.
        ** Returns with only the table at the stack.
        */
        public static void luaL_setfuncs (lua_State L, luaL_Reg[] l, int nup) {
            luaL_checkstack (L, nup, "too many upvalues");
            for (int i = 0; i < l.Length; i++) {  /* fill the table with given functions */
                for (int k = 0; k < nup; k++)  /* copy upvalues to the top */
                    lua_pushvalue (L, -nup);
                lua_pushcclosure (L, l[i].func, nup);  /* closure with those upvalues */
                lua_setfield (L, -(nup + 2), l[i].name);
            }
            lua_pop (L, nup);  /* remove upvalues */
        }


        /*
        ** ensure that stack[idx][fname] has a table and push that table
        ** into the stack
        */
        public static int luaL_getsubtable (lua_State L, int idx, string fname) {
            if (lua_getfield (L, idx, fname) == LUA_TTABLE)
                return 1;  /* table already there */
            else {
                lua_pop (L, 1);  /* remove previous result */
                idx = lua_absindex (L, idx);
                lua_newtable (L);
                lua_pushvalue (L, -1);  /* copy to be left at top */
                lua_setfield (L, idx, fname);  /* assign new table to field */
                return 0;  /* false, because did not find table there */
            }
        }


        /*
        ** Stripped-down 'require': After checking "loaded" table, calls 'openf'
        ** to open a module, registers the result in 'package.loaded' table and,
        ** if 'glb' is true, also registers the result in the global table.
        ** Leaves resulting module on the top.
        */
        public static void luaL_requiref (lua_State L, string modname, lua_CFunction openf, int glb) {
            luaL_getsubtable (L, LUA_REGISTRYINDEX, "_LOADED");
            lua_getfield (L, -1, modname);  /* _LOADED[modname] */
            if (lua_toboolean (L, -1) == 0) {  /* package not already loaded? */
                lua_pop (L, 1);  /* remove field */
                lua_pushcfunction (L, openf);
                lua_pushstring (L, modname);  /* argument to open function */
                lua_call (L, 1, 1);  /* call 'openf' to open module */
                lua_pushvalue (L, -1);  /* make copy of module (call result) */
                lua_setfield (L, -3, modname);  /* _LOADED[modname] = module */
            }
            lua_remove (L, -2);  /* remove _LOADED table */
            if (glb != 0) {
                lua_pushvalue (L, -1);  /* copy of module */
                lua_setglobal (L, modname);  /* _G[modname] = module */
            }
        }


        public static string luaL_gsub (lua_State L, string s, string p, string r) {
            // TODO
            return null;
        }


        public static lua_State luaL_newstate () {
            lua_State L = lua_newstate (new imp.l_alloc (), null);
            if (L == null) lua_atpanic (L, imp.panic);
            return L;
        }


        public static void luaL_checkversion_ (lua_State L, double ver, int sz) {
            double v = lua_version (L);
            if (sz != LUAL_NUMSIZES)
                luaL_error (L, "core and library have incompatible numeric types");
            if (v != lua_version (null))
                luaL_error (L, "multiple Lua VMs detected");
            else if (v != ver)
                luaL_error (L, "version mismatch: app. needs %f, Lua core provides %f", ver, v);
        }
    }
}
