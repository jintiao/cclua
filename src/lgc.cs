using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	/*
	** Collectable objects may have one of three colors: white, which
	** means the object is not marked; gray, which means the
	** object is marked, but its references may be not marked; and
	** black, which means that the object and all its references are marked.
	** The main invariant of the garbage collector, while marking objects,
	** is that a black object can never point to a white one. Moreover,
	** any gray object must be in a "gray list" (gray, grayagain, weak,
	** allweak, ephemeron) so that it can be visited again before finishing
	** the collection cycle. These lists have no meaning when the invariant
	** is not being enforced (e.g., sweep phase).
	*/

    public static partial class imp {

		private static class lgc {

            public const int OBJ_SIZE = 32;
            public const int REF_SIZE = 8;

            /*
            ** internal state for collector while inside the atomic phase. The
            ** collector should never be in this state while running regular code.
            */
            public const int GCSinsideatomic = GCSpause + 1;


            /*
            ** cost of sweeping one element (the size of a small object divided
            ** by some adjust for the sweep speed)
            */
            public const int GCSWEEPCOST = (OBJ_SIZE + 4) / 4;

            /* maximum number of elements to sweep in each single step */
            public static int GCSWEEPMAX = ((GCSTEPSIZE / GCSWEEPCOST) / 4);

            /* cost of calling one finalizer */
            public const int GCFINALIZECOST = GCSWEEPCOST;


            /*
            ** macro to adjust 'stepmul': 'stepmul' is actually used like
            ** 'stepmul / STEPMULADJ' (value chosen by tests)
            */
            public const int STEPMULADJ = 200;


            /*
            ** macro to adjust 'pause': 'pause' is actually used like
            ** 'pause / PAUSEADJ' (value chosen by tests)
            */
            public const int PAUSEADJ = 100;


			/*
			** 'makewhite' erases all color bits then sets only the current white
			** bit
			*/
			public static int maskcolors = (~(bitmask(BLACKBIT) | WHITEBITS));

			public static void makewhite (global_State g, GCObject x) {
				x.marked = (byte)((x.marked & maskcolors) | luaC_white (g));
			}

			public static void white2gray (GCObject x) { resetbits (ref x.marked, WHITEBITS); }
			public static void black2gray (GCObject x) { resetbit (ref x.marked, BLACKBIT); }


			public static bool valiswhite (TValue x) { return (iscollectable (x) && iswhite (gcvalue (x))); }

			public static void checkdeadkey (Node n) { lua_assert (ttisdeadkey (n.i_key.tvk) == false || ttisnil (n.i_val)); }


			public static void checkconsistency (TValue obj) { lua_longassert (iscollectable (obj) == false || righttt (obj)); }


			public static void markvalue (global_State g, TValue o) {
				checkconsistency (o);
				if (valiswhite (o)) reallymarkobject (g, gcvalue (o));
			}

			public static void markobject (global_State g, GCObject t) {
				if (t != null && iswhite (t)) reallymarkobject (g, obj2gco (t));
			}



			/*
			** {======================================================
			** Generic functions
			** =======================================================
			*/


            /*
            ** one after last element in a hash array
            */
            public static Node gnodelast (Table h) { return h.node[sizenode (h) - 1]; }


			/*
			** if key is not marked, mark its entry as dead (therefore removing it
			** from the table)
			*/
			public static void removeentry (Node n) {
				lua_assert (ttisnil (n.i_val));
				if (valiswhite (n.i_key.tvk))
					setdeadvalue (n.i_key.tvk);  /* unused and unmarked key; remove it */
			}


			/*
			** tells whether a key or value can be cleared from a weak
			** table. Non-collectable objects are never removed from weak
			** tables. Strings behave as 'values', so are never removed too. for
			** other objects: if really collected, cannot keep them; for objects
			** being finalized, keep them in keys, but not in values
			*/
			public static bool iscleared (global_State g, TValue o) {
				if (iscollectable (o) == false) return false;
				else if (ttisstring (o)) {
					markobject (g, tsvalue (o));  /* strings are 'values', so are never weak */
					return false;
				}
				else return iswhite (gcvalue (o));
			}


			/*
			** barrier that moves collector forward, that is, mark the white object
			** being pointed by a black object. (If in sweep phase, clear the black
			** object to white [sweep it] to avoid other barrier calls for this
			** same object.)
			*/
			public static void luaC_barrier_ (lua_State L, GCObject o, GCObject v) {
				global_State g = G (L);
				lua_assert (isblack (o) && iswhite (v) && isdead (g, v) == false && isdead (g, o) == false);
				if (keepinvariant (g))  /* must keep invariant? */
					reallymarkobject (g, v);  /* restore invariant */
				else {  /* sweep phase */
					lua_assert (issweepphase (g));
					makewhite (g, o);  /* mark main obj. as white to avoid other barriers */
				}
			}


			/*
			** barrier that moves collector backward, that is, mark the black object
			** pointing to a white object as gray again.
			*/
			public static void luaC_barrierback_ (lua_State L, Table t) {
				global_State g = G (L);
				lua_assert (isblack (t) && isdead (g, t) == false);
				black2gray (t);  /* make table gray (again) */
				t.gclist = g.grayagain;
				g.grayagain = obj2gco (t);
			}


			/*
			** barrier for assignments to closed upvalues. Because upvalues are
			** shared among closures, it is impossible to know the color of all
			** closures pointing to it. So, we assume that the object being assigned
			** must be marked.
			*/
			public static void luaC_upvalbarrier_ (lua_State L, UpVal uv) {
				global_State g = G (L);
				GCObject o = gcvalue (uv.v);
				lua_assert (upisopen (uv) == false);  /* ensured by macro luaC_upvalbarrier */
				if (keepinvariant (g))
					markobject (g, o);
			}



			/*
			** {======================================================
			** Mark functions
			** =======================================================
			*/


			/*
			** mark an object. Userdata, strings, and closed upvalues are visited
			** and turned black here. Other objects are marked gray and added
			** to appropriate list to be visited (and turned black) later. (Open
			** upvalues are already linked in 'headuv' list.)
			*/


			public static void reallymarkobject (global_State g, GCObject o) {
                reentry:
                    white2gray (o);
                    switch (o.tt) {
                        case LUA_TSHRSTR: goto case LUA_TLNGSTR;
                        case LUA_TLNGSTR: {
                            gray2black (o);
                            g.GCmemtrav += OBJ_SIZE;
                            break;
                        }
                        case cc.LUA_TUSERDATA: {
                            markobject (g, gco2u (o).metatable);  /* mark its metatable */
                            gray2black (o);
                            g.GCmemtrav += OBJ_SIZE;
                            TValue uvalue = new TValue ();
                            getuservalue (g.mainthread, gco2u (o), uvalue);
                            if (valiswhite (uvalue)) {  /* markvalue(g, &uvalue); */
                                o = gcvalue (uvalue);
                                goto reentry;
                            }
                            break;
                            }
                        case LUA_TLCL: {
                            LClosure v = gco2lcl (o);
                            v.gclist = g.gray;
                            g.gray = v;
                            break;
                        }
                        case LUA_TCCL: {
                            CClosure v = gco2ccl (o);
                            v.gclist = g.gray;
                            g.gray = v;
                            break;
                        }
                        case cc.LUA_TTABLE: {
                            Table v = gco2t (o);
                            v.gclist = g.gray;
                            g.gray = v;
                            break;
                        }
                        case cc.LUA_TTHREAD: {
                            lua_State v = gco2th (o);
                            v.gclist = g.gray;
                            g.gray = v;
                            break;
                        }
                        case LUA_TPROTO: {
                            Proto v = gco2p (o);
                            v.gclist = g.gray;
                            g.gray = v;
                            break;
                        }
                        default: lua_assert (false); break;
                    }
			}


            /*
            ** mark metamethods for basic types
            */
            public static void markmt (global_State g) {
                for (int i = 0; i < cc.LUA_NUMTAGS; i++)
                    markobject (g, g.mt[i]);
            }


            /*
            ** mark all objects in list of being-finalized
            */
            public static void markbeingfnz (global_State g) {
                for (GCObject o = g.tobefnz; o != null; o = o.next)
                    markobject (g, o);
            }


            /*
            ** Mark all values stored in marked open upvalues from non-marked threads.
            ** (Values from marked threads were already marked when traversing the
            ** thread.) Remove from the list threads that no longer have upvalues and
            ** not-marked threads.
            */
            public static void remarkupvals (global_State g) {
                lua_State thread = g.twups;
                lua_State p = g.twups;
                while (thread != null) {
                    lua_assert (isblack (thread) == false);  /* threads are never black */
                    if (isgray (thread) && thread.openupval != null)
                        p = thread.twups;  /* keep marked thread with upvalues in the list */
                    else {  /* thread is not marked or without upvalues */
                        p = thread.twups;  /* remove thread from the list */
                        thread.twups = thread;  /* mark that it is out of list */
                        for (UpVal uv = thread.openupval; uv != null; uv = uv.u.open.next) {
                            if (uv.u.open.touched != 0) {
                                markvalue (g, uv.v);  /* remark upvalue's value */
                                uv.u.open.touched = 0;
                            }
                        }
                    }
                }
            }


            /*
            ** mark root set and reset all gray lists, to start a new collection
            */
            public static void restartcollection (global_State g) {
                g.gray = null;
                g.grayagain = null;
                g.weak = null;
                g.allweak = null;
                g.ephemeron = null;
                markobject (g, g.mainthread);
                markvalue (g, g.l_registry);
                markmt (g);
                markbeingfnz (g);  /* mark any finalizing object left from previous cycle */
            }



            /*
            ** {======================================================
            ** Traverse functions
            ** =======================================================
            */


            /*
            ** Traverse a table with weak values and link it to proper list. During
            ** propagate phase, keep it in 'grayagain' list, to be revisited in the
            ** atomic phase. In the atomic phase, if table has any white value,
            ** put it in 'weak' list, to be cleared.
            */
            public static void traverseweakvalue (global_State g, Table h) {
                /* if there is array part, assume it may have white values (it is not
                    worth traversing it now just to check) */
                bool hasclears = (h.sizearray > 0);
                for (int i = 0; i < sizenode (h); i++ ) {  /* traverse hash part */
                    Node n = h.node[i];
                    checkdeadkey (n);
                    if (ttisnil (n.i_val))  /* entry is empty? */
                        removeentry (n);  /* remove it */
                    else {
                        lua_assert (ttisnil (n.i_key.tvk) == false);
                        markvalue (g, n.i_key.tvk);  /* mark key */
                        if (hasclears == false && iscleared (g, n.i_val))  /* is there a white value? */
                            hasclears = true;  /* table will have to be cleared */
                    }
                }
                if (g.gcstate == GCSpropagate) {
                    h.gclist = g.grayagain;  /* must retraverse it in atomic phase */
                    g.grayagain = h;
                }
                else if (hasclears) {
                    h.gclist = g.weak;  /* has to be cleared later */
                    g.weak = h;
                }
            }


            /*
            ** Traverse an ephemeron table and link it to proper list. Returns true
            ** iff any object was marked during this traversal (which implies that
            ** convergence has to continue). During propagation phase, keep table
            ** in 'grayagain' list, to be visited again in the atomic phase. In
            ** the atomic phase, if table has any white->white entry, it has to
            ** be revisited during ephemeron convergence (as that key may turn
            ** black). Otherwise, if it has any white key, table has to be cleared
            ** (in the atomic phase).
            */
            public static bool traverseephemeron (global_State g, Table h) {
                bool marked = false;  /* true if an object is marked in this traversal */
                bool hasclears = false;  /* true if table has white keys */
                bool hasww = false;  /* true if table has entry "white-key -> white-value" */
                /* traverse array part */
                for (int i = 0; i < h.sizearray; i++) {
                    if (valiswhite (h.array[i])) {
                        marked = true;
                        reallymarkobject (g, gcvalue (h.array[i]));
                    }
                }
                /* traverse hash part */
                for (int i = 0; i < sizenode (h); i++) {
                    Node n = h.node[i];
                    checkdeadkey (n);
                    if (ttisnil (n.i_val))  /* entry is empty? */
                        removeentry (n);  /* remove it */
                    else if (iscleared (g, n.i_key.tvk)) {  /* key is not marked (yet)? */
                        hasclears = true;  /* table must be cleared */
                        if (valiswhite (n.i_val))  /* value not marked yet? */
                            hasww = true;  /* white-white entry */
                    }
                    else if (valiswhite (n.i_val)) {  /* value not marked yet? */
                        marked = true;
                        reallymarkobject (g, gcvalue (n.i_val));  /* mark it now */
                    }
                }
                /* link table into proper list */
                if (g.gcstate == GCSpropagate) {
                    h.gclist = g.grayagain;  /* must retraverse it in atomic phase */
                    g.grayagain = h;
                }
                else if (hasww) {  /* table has white->white entries? */
                    h.gclist = g.ephemeron;  /* have to propagate again */
                    g.ephemeron = h;
                }
                else if (hasclears) {  /* table has white keys? */
                    h.gclist = g.allweak;  /* may have to clean white keys */
                    g.allweak = h;
                }
                return marked;
            }


            public static void traversestrongtable (global_State g, Table h) {
                for (int i = 0; i < h.sizearray; i++) {  /* traverse array part */
                    markvalue (g, h.array[i]);
                }
                for (int i = 0; i < sizenode (h); i++) {  /* traverse hash part */
                    Node n = h.node[i];
                    checkdeadkey (n);
                    if (ttisnil (n.i_val))  /* entry is empty? */
                        removeentry (n);  /* remove it */
                    else {
                        lua_assert (ttisnil (n.i_key.tvk) == false);
                        markvalue (g, n.i_key.tvk);  /* mark key */
                        markvalue (g, n.i_val);  /* mark value */
                    }
                }
            }


            public static int strchr (byte[] str, byte c) {
                for (int i = 0; i < str.Length; i++) {
                    if (str[i] == c) return (i + 1);
                }
                return 0;
            }

            public static long traversetable (global_State g, Table h) {
                TValue mode = gfasttm (g, h.metatable, TMS.TM_MOD);
                markobject (g, h.metatable);
                if (mode != null && ttisstring (mode)) {  /* is there a weak mode? */
                    int weakkey = strchr (svalue (mode), (byte)'k');
                    int weakvalue = strchr (svalue (mode), (byte)'v');
                    if (weakkey != 0 || weakvalue != 0) {  /* is really weak? */
                        black2gray (h);  /* keep table gray */
                        if (weakkey == 0)  /* strong keys? */
                            traverseweakvalue (g, h);
                        else if (weakvalue == 0)  /* strong values? */
                            traverseephemeron (g, h);
                        else {  /* all weak */
                            h.gclist = g.allweak;  /* nothing to traverse now */
                            g.allweak = h;
                        }
                    }
                }
                else  /* not weak */
                    traversestrongtable (g, h);
                return OBJ_SIZE;
            }


            public static int traverseproto (global_State g, Proto f) {
                if (f.cache != null && iswhite (f.cache))
                    f.cache = null;  /* allow cache to be collected */
                markobject (g, f.source);
                for (int i = 0; i < f.sizek; i++)  /* mark literals */
                    markvalue (g, f.k[i]);
                for (int i = 0; i < f.sizeupvalues; i++)  /* mark upvalue names */
                    markobject (g, f.upvalues[i].name);
                for (int i = 0; i < f.sizep; i++)  /* mark nested protos */
                    markobject (g, f.p[i]);
                for (int i = 0; i < f.sizelocvars; i++)  /* mark local-variable names */
                    markobject (g, f.locvars[i].varname);
                return OBJ_SIZE;
            }


            public static long traverseCclosure (global_State g, CClosure cl) {
                for (int i = 0; i < cl.nupvalues; i++)  /* mark its upvalues */
                    markvalue (g, cl.upvalue[i]);
                return OBJ_SIZE;
            }


            /*
            ** open upvalues point to values in a thread, so those values should
            ** be marked when the thread is traversed except in the atomic phase
            ** (because then the value cannot be changed by the thread and the
            ** thread may not be traversed again)
            */
            public static long traverseLclosure (global_State g, LClosure cl) {
                markobject (g, cl.p);  /* mark its prototype */
                for (int i = 0; i < cl.nupvalues; i++) {  /* mark its upvalues */
                    UpVal uv = cl.upvals[i];
                    if (uv != null) {
                        if (upisopen (uv) && g.gcstate != GCSinsideatomic)
                            uv.u.open.touched = 1;  /* can be marked in 'remarkupvals' */
                        else
                            markvalue (g, uv.v);
                    }
                }
                return OBJ_SIZE;
            }


            public static long traversethread (global_State g, lua_State th) {
                if (th.stack == null)
                    return 1;  /* stack not completely built yet */
                lua_assert (g.gcstate == GCSinsideatomic ||
                                th.openupval == null || isintwups (th));
                int i = 0;
                for (; i < th.top; i++)  /* mark live elements in the stack */
                    markvalue (g, th.stack[i]);
                if (g.gcstate == GCSinsideatomic) {  /* final traversal? */
                    for (; i < th.stacksize; i++)  /* clear not-marked stack slice */
                        setnilvalue (th, i);
                    /* 'remarkupvals' may have removed thread from 'twups' list */
                    if (isintwups (th) == false && th.openupval != null) {
                        th.twups = g.twups;  /* link it back to the list */
                        g.twups = th;
                    }
                }
                else if (g.gckind != KGC_EMERGENCY)
                    luaD_shrinkstack (th);  /* do not change stack in emergency cycle */
                return OBJ_SIZE;
            }


            /*
            ** traverse one gray object, turning it to black (except for threads,
            ** which are always gray).
            */
            public static void propagatemark (global_State g) {
                long size = 0;
                GCObject o = g.gray;
                lua_assert (isgray (o));
                gray2black (o);
                switch (o.tt) {
                    case cc.LUA_TTABLE: {
                        Table h = gco2t (o);
                        g.gray = h.gclist;  /* remove from 'gray' list */
                        size = traversetable (g, h);
                        break;
                        }
                    case LUA_TLCL: {
                        LClosure cl = gco2lcl (o);
                        g.gray = cl.gclist;  /* remove from 'gray' list */
                        size = traverseLclosure (g, cl);
                            break;
                        }
                    case LUA_TCCL: {
                        CClosure cl = gco2ccl (o);
                            g.gray = cl.gclist;  /* remove from 'gray' list */
                            size = traverseCclosure (g, cl);
                            break;
                        }
                    case cc.LUA_TTHREAD: {
                        lua_State th = gco2th (o);
                        g.gray = th.gclist;  /* remove from 'gray' list */
                        th.gclist = g.grayagain;  /* insert into 'grayagain' list */
                        g.grayagain = th;
                        size = traversethread (g, th);
                            break;
                        }
                    case LUA_TPROTO: {
                        Proto p = gco2p (o);
                        g.gray = p.gclist;  /* remove from 'gray' list */
                        size = traverseproto (g, p);
                            break;
                        }
                    default: lua_assert (false); return;
                }
                g.GCmemtrav += size;
            }


            public static void propagateall (global_State g) {
                while (g.gray != null) propagatemark (g);
            }


            public static void convergeephemerons (global_State g) {
                bool changed = false;
                do {
                    GCObject next = g.ephemeron;  /* get ephemeron list */
                    g.ephemeron = null;  /* tables may return to this list when traversed */
                    changed = false;
                    GCObject w = next;
                    while (w != null) {
                        next = gco2t (w).gclist;
                        if (traverseephemeron (g, gco2t (w))) {  /* traverse marked some value? */
                            propagateall (g);  /* propagate changes */
                            changed = true;  /* will have to revisit all ephemeron tables */
                            w = next;
                        }
                    }
                } while (changed);
            }





            /*
            ** {======================================================
            ** Sweep Functions
            ** =======================================================
            */


            /*
            ** clear entries with unmarked keys from all weaktables in list 'l' up
            ** to element 'f'
            */
            static void clearkeys (global_State g, GCObject l, GCObject f) {
                for (; l != f; l = gco2t (l).gclist) {
                    Table h = gco2t (l);
                    for (int i = 0; i < sizenode (h); i++) {
                        Node n = h.node[i];
                        if (ttisnil (n.i_val) == false && iscleared (g, n.i_key.tvk)) {
                            setnilvalue (n.i_val);  /* remove value ... */
                            removeentry (n);  /* and remove entry from table */
                        }
                    }
                }
            }

            /*
            ** clear entries with unmarked values from all weaktables in list 'l' up
            ** to element 'f'
            */
            static void clearvalues (global_State g, GCObject l, GCObject f) {
                for (; l != f; l = gco2t (l).gclist) {
                    Table h = gco2t (l);
                    for (int i = 0; i < h.sizearray; i++) {
                        TValue o = h.array[i];
                        if (iscleared (g, o))  /* value was collected? */
                            setnilvalue (o);  /* remove value */
                    }
                    for (int i = 0; i < sizenode (h); i++) {
                        Node n = h.node[i];
                        if (ttisnil (n.i_val) == false && iscleared (g, n.i_key.tvk)) {
                            setnilvalue (n.i_val);  /* remove value ... */
                            removeentry (n);  /* and remove entry from table */
                        }
                    }
                }
            }


            public static void freeLclosure (lua_State L, LClosure cl) {
                for (int i = 0; i < cl.nupvalues; i++) {
                    UpVal uv = cl.upvals[i];
                    if (uv != null)
                        luaC_upvdeccount (L, uv);
                }
                luaM_free (L, cl);
            }


            public static void freeobj (lua_State L, GCObject o) {
                switch (o.tt) {
                    case LUA_TPROTO: luaF_freeproto (L, gco2p (o)); break;
                    case LUA_TLCL: freeLclosure (L, gco2lcl (o)); break;
                    case LUA_TCCL:  luaM_free (L, o); break;
                    case cc.LUA_TTABLE: luaH_free (L, gco2t (o)); break;
                    case cc.LUA_TTHREAD: luaE_freethread (L, gco2th (o)); break;
                    case cc.LUA_TUSERDATA: luaM_free (L, o); break;
                    case LUA_TSHRSTR: luaS_remove (L, gco2ts (o)); goto case LUA_TLNGSTR;
                    case LUA_TLNGSTR: luaM_free (L, o); break;
                    default: lua_assert (false); return;
                }
            }


            public static void sweepwholelist (lua_State L, GCObject p) { sweeplist (L, p, MAX_LMEM); }


            /*
            ** sweep at most 'count' elements from a list of GCObjects erasing dead
            ** objects, where a dead object is one marked with the old (non current)
            ** white; change all non-dead objects back to white, preparing for next
            ** collection cycle. Return where to continue the traversal or NULL if
            ** list is finished.
            */
            public static GCObject sweeplist (lua_State L, GCObject p, long count) {
                global_State g = G (L);
                int ow = otherwhite (g);
                int white = luaC_white (g);  /* current white */
                while (p != null && (count-- > 0)) {
                    GCObject curr = p;
                    int marked = curr.marked;
                    if (isdeadm (ow, marked)) {  /* is 'curr' dead? */
                        p = curr.next;  /* remove 'curr' from list */
                        freeobj (L, curr);  /* erase 'curr' */
                    }
                    else {  /* change mark to 'white' */
                        curr.marked = (byte)((marked & maskcolors) | white);
                        p = curr.next;  /* go to next element */
                    }
                }
                return (p == null) ? null : p;
            }


            /*
            ** sweep a list until a live object (or end of list)
            */
            public static GCObject sweeptolive (lua_State L, GCObject p, ref long n) {
                GCObject old = p;
                int i = 0;
                do {
                    i++;
                    p = sweeplist (L, p, 1);
                } while (p == old);
                n += i;
                return p;
            }




            /*
            ** {======================================================
            ** Finalization
            ** =======================================================
            */


            /*
            ** If possible, free concatenation buffer and shrink string table
            */
            public static void checkSizes (lua_State L, global_State g) {
                if (g.gckind != KGC_EMERGENCY) {
                    long olddebt = g.GCdebt;
                    luaZ_freebuffer (L, g.buff);  /* free concatenation buffer */
                    if (g.strt.nuse < g.strt.size / 4)  /* string table too big? */
                        luaS_resize (L, g.strt.size / 2);  /* shrink it a little */
                    g.GCestimate += g.GCdebt - olddebt;  /* update estimate */
                }
            }


            public static GCObject udata2finalize (global_State g) {
                GCObject o = g.tobefnz;  /* get first element */
                lua_assert (tofinalize (o));
                g.tobefnz = o.next;  /* remove it from 'tobefnz' list */
                o.next = g.allgc;  /* return it to 'allgc' list */
                g.allgc = o;
                resetbit (ref o.marked, FINALIZEDBIT);  /* object is "normal" again */
                if (issweepphase (g))
                    makewhite (g, o);  /* "sweep" object */
                return o;
            }


            public static void dothecall (lua_State L, object ud) {
                luaD_call (L, L.top - 2, 0, 0);
            }


            public static void GCTM (lua_State L, int propagateerrors) {
                global_State g = G (L);
                TValue v = new TValue ();
                setgcovalue (L, v, udata2finalize (g));
                TValue tm = luaT_gettmbyobj (L, v, TMS.TM_GC);
                if (tm != null && ttisfunction (tm)) {  /* is there a finalizer? */
                    byte oldah = L.allowhook;
                    byte running = g.gcrunning;
                    L.allowhook = 0;  /* stop debug hooks during GC metamethod */
                    g.gcrunning = 0;  /* avoid GC steps */
                    setobj2s (L, L.top, tm);  /* push finalizer... */
                    setobj2s (L, L.top + 1, v);  /* ... and its argument */
                    L.top += 2;  /* and (next line) call the finalizer */
                    int status = luaD_pcall (L, dothecall, null, savestack (L, L.top - 2), 0);
                    L.allowhook = oldah;  /* restore hooks */
                    g.gcrunning = running;  /* restore state */
                    if (status != cc.LUA_OK && propagateerrors != 0) {  /* error while running __gc? */
                        if (status == cc.LUA_ERRRUN) {  /* is there an error object? */
                            string msg = (ttisstring (L, L.top - 1)) ? byte2str (svalue (L, L.top - 1)) : "no message";
                            luaO_pushfstring (L, "error in __gc metamethod (%s)", msg);
                            status = cc.LUA_ERRGCMM;  /* error in __gc metamethod */
                        }
                        luaD_throw (L, status);  /* re-throw error */
                    }
                }
            }


            /*
            ** call a few (up to 'g->gcfinnum') finalizers
            */
            public static int runafewfinalizers (lua_State L) {
                global_State g = G (L);
                lua_assert (g.tobefnz == null || g.gcfinnum > 0);
                int i = 0;
                for (; g.tobefnz != null && i < g.gcfinnum; i++) {
                    GCTM (L, 1);  /* call one finalizer */
                    g.gcfinnum = (g.tobefnz == null) ? 0  /* nothing more to finalize? */
                                        : g.gcfinnum * 2;  /* else call a few more next time */
                }
                return i;
            }


            /*
            ** call all pending finalizers
            */
            public static void callallpendingfinalizers (lua_State L, int propagateerrors) {
                global_State g = G (L);
                while (g.tobefnz != null)
                    GCTM (L, propagateerrors);
            }


            /*
            ** find last 'next' field in list 'p' list (to add elements in its end)
            */
            public static GCObject findlast (GCObject p) {
                while (p != null)
                    p = p.next;
                return p;
            }


            /*
            ** move all unreachable objects (or 'all' objects) that need
            ** finalization from list 'finobj' to list 'tobefnz' (to be finalized)
            */
            public static void separatetobefnz (global_State g, bool all) {
                GCObject p = g.finobj;
                GCObject lastnext = findlast (g.tobefnz);
                GCObject curr = p;
                while (curr != null) {  /* traverse all finalizable objects */
                    lua_assert (tofinalize (curr));
                    if ((iswhite (curr) || all) == false)  /* not being collected? */
                        p = curr.next;  /* don't bother with it */
                    else {
                        p = curr.next;  /* remove 'curr' from 'finobj' list */
                        curr.next = lastnext;  /* link at the end of 'tobefnz' list */
                        lastnext = curr;
                        lastnext = curr.next;
                    }
                    curr = p;
                }
            }




            /*
            ** {======================================================
            ** GC control
            ** =======================================================
            */


            /*
            ** Set a reasonable "time" to wait before starting a new GC cycle; cycle
            ** will start when memory use hits threshold. (Division by 'estimate'
            ** should be OK: it cannot be zero (because Lua cannot even start with
            ** less than PAUSEADJ bytes).
            */
            public static void setpause (global_State g) {
                long estimate = g.GCestimate / PAUSEADJ;  /* adjust 'estimate' */
                lua_assert (estimate > 0);
                long threshold = (g.gcpause < MAX_LMEM / estimate)  /* overflow? */
                                  ? estimate * g.gcpause  /* no overflow */
                                  : MAX_LMEM;  /* overflow; truncate to maximum */
                long debt = gettotalbytes (g) - threshold;
                luaE_setdebt (g, debt);
            }

			
			/*
			** Enter first sweep phase.
			** The call to 'sweeptolive' makes pointer point to an object inside
			** the list (instead of to the header), so that the real sweep do not
			** need to skip objects created between "now" and the start of the real
			** sweep.
			** Returns how many objects it swept.
			*/
            public static long entersweep (lua_State L) {
				global_State g = G (L);
				g.gcstate = GCSswpallgc;
				lua_assert (g.sweepgc == null);
				long n = 0;
				g.sweepgc = sweeptolive (L, g.allgc, ref n);
				return n;
			}


            public static long atomic (lua_State L) {
                global_State g = G (L);
                long work = 0;
                GCObject grayagain = g.grayagain;  /* save original list */
                lua_assert (g.ephemeron == null && g.weak == null);
                lua_assert (iswhite (g.mainthread) == false);
                g.gcstate = GCSinsideatomic;
                g.GCmemtrav = 0;  /* start counting work */
                markobject (g, L);  /* mark running thread */
                /* registry and global metatables may be changed by API */
                markvalue (g, g.l_registry);
                markmt (g);  /* mark global metatables */
                /* remark occasional upvalues of (maybe) dead threads */
                remarkupvals (g);
                propagateall (g);  /* propagate changes */
                work = g.GCmemtrav;  /* stop counting (do not recount 'grayagain') */
                g.gray = grayagain;
                propagateall (g);  /* traverse 'grayagain' list */
                g.GCmemtrav = 0;  /* restart counting */
                convergeephemerons (g);
                /* at this point, all strongly accessible objects are marked. */
                /* Clear values from weak tables, before checking finalizers */
                clearvalues (g, g.weak, null);
                clearvalues (g, g.allweak, null);
                GCObject origweak = g.weak;
                GCObject origall = g.allweak;
                work += g.GCmemtrav;  /* stop counting (objects being finalized) */
                separatetobefnz (g, false);  /* separate objects to be finalized */
                g.gcfinnum = 1;  /* there may be objects to be finalized */
                markbeingfnz (g);  /* mark objects that will be finalized */
                propagateall (g);  /* remark, to propagate 'resurrection' */
                g.GCmemtrav = 0;  /* restart counting */
                convergeephemerons (g);
                /* at this point, all resurrected objects are marked. */
                /* remove dead objects from weak tables */
                clearkeys (g, g.ephemeron, null);  /* clear keys from all ephemeron tables */
                clearkeys (g, g.allweak, null);  /* clear keys from all 'allweak' tables */
                /* clear values from resurrected weak tables */
                clearvalues (g, g.weak, origweak);
                clearvalues (g, g.allweak, origall);
                g.currentwhite = (byte)(otherwhite (g));  /* flip current white */
                work += g.GCmemtrav;  /* complete counting */
                return work;  /* estimate of memory marked by 'atomic' */
			}


			public static long sweepstep (lua_State L, global_State g, int nextstate, GCObject nextlist) {
				if (g.sweepgc != null) {
					long olddebt = g.GCdebt;
					g.sweepgc = sweeplist (L, g.sweepgc, GCSWEEPMAX);
					g.GCestimate += g.GCdebt - olddebt;  /* update estimate */
					if (g.sweepgc != null)  /* is there still something to sweep? */
						return (GCSWEEPMAX * GCSWEEPCOST);
				}
				/* else enter next state */
				g.gcstate = (byte)nextstate;
				g.sweepgc = nextlist;
                return 0;
			}


            public static long singlestep (lua_State L) {
                global_State g = G (L);
                switch (g.gcstate) {
                    case GCSpause: {
                        g.GCmemtrav = g.strt.size * REF_SIZE;
                        restartcollection (g);
                        g.gcstate = GCSpropagate;
                        return g.GCmemtrav;
                    }
                    case GCSpropagate: {
                        g.GCmemtrav = 0;
                        lua_assert (g.gray != null);
                        propagatemark (g);
                        if (g.gray == null)  /* no more gray objects? */
                            g.gcstate = GCSatomic;  /* finish propagate phase */
                        return g.GCmemtrav;  /* memory traversed in this step */
                    }
                    case GCSatomic: {
                        propagateall (g);  /* make sure gray list is empty */
                        long work = atomic (L);  /* work is what was traversed by 'atomic' */
                        long sw = entersweep (L);
                        g.GCestimate = gettotalbytes (g);  /* first estimate */;
                        return work + sw * GCSWEEPCOST;
                    }
                    case GCSswpallgc: {  /* sweep "regular" objects */
                        return sweepstep (L, g, GCSswpfinobj, g.finobj);
                    }
                    case GCSswpfinobj: {  /* sweep objects with finalizers */
                        return sweepstep (L, g, GCSswptobefnz, g.tobefnz);
                    }
                    case GCSswptobefnz: {  /* sweep objects to be finalized */
                        return sweepstep (L, g, GCSswpend, null);
                    }
                    case GCSswpend: {  /* finish sweeps */
                        makewhite (g, g.mainthread);  /* sweep main thread */
                        checkSizes (L, g);
                        g.gcstate = GCScallfin;
                        return 0;
                    }
                    case GCScallfin: {  /* call remaining finalizers */
                        if (g.tobefnz != null && g.gckind != KGC_EMERGENCY) {
                            int n = runafewfinalizers (L);
                            return (n * GCFINALIZECOST);
                        }
                        else {  /* emergency mode or no more finalizers */
                            g.gcstate = GCSpause;  /* finish collection */
                            return 0;
                        }
                    }
                    default: {
                        lua_assert (false); return 0;
                    }
                }
            }


            /*
            ** get GC debt and convert it from Kb to 'work units' (avoid zero debt
            ** and overflows)
            */
            public static long getdebt (global_State g) {
                long debt = g.GCdebt;
                int stepmul = g.gcstepmul;
                debt = (debt / STEPMULADJ) + 1;
                debt = (debt < (MAX_LMEM / stepmul)) ? debt * stepmul : MAX_LMEM;
                return debt;
            }
		}

		/* how much to allocate before next GC step */
        public static int GCSTEPSIZE = 100 * 32;  /* ~100 small strings */


		/*
		** Possible states of the Garbage Collector
		*/
		public const int GCSpropagate = 0;
		public const int GCSatomic = 1;
		public const int GCSswpallgc = 2;
		public const int GCSswpfinobj = 3;
		public const int GCSswptobefnz = 4;
		public const int GCSswpend = 5;
		public const int GCScallfin = 6;
		public const int GCSpause = 7;


		public static bool issweepphase (global_State g) { return (GCSswpallgc <= g.gcstate && g.gcstate <= GCSswpend); }


		/*
		** macro to tell when main invariant (white objects cannot point to black
		** ones) must be kept. During a collection, the sweep
		** phase may break the invariant, as objects turned white may point to
		** still-black objects. The invariant is restored when sweep ends and
		** all objects are white again.
		*/
		public static bool keepinvariant (global_State g) { return (g.gcstate <= GCSatomic); }


		/*
		** some useful bit tricks
		*/
        public static void resetbits (ref byte x, byte m) { x &= (byte)(~m); }
        public static void setbits (ref byte x, byte m) { x |= m; }
        public static bool testbits (byte x, byte m) { return ((x & m) != 0); }
        public static byte bitmask (byte b) { return (byte)(1 << b); }
        public static byte bit2mask (byte b1, byte b2) { return (byte)(bitmask (b1) | bitmask (b2)); }
        public static void l_setbit (ref byte x, byte b) { setbits (ref x, bitmask (b)); }
        public static void resetbit (ref byte x, byte b) { resetbits (ref x, bitmask (b)); }
        public static bool testbit (byte x, byte b) { return testbits (x, bitmask (b)); }


		/* Layout for bit use in 'marked' field: */
        public const byte WHITE0BIT = 0;  /* object is white (type 0) */
        public const byte WHITE1BIT = 1;  /* object is white (type 1) */
        public const byte BLACKBIT = 2;  /* object is black */
        public const byte FINALIZEDBIT = 3;  /* object has been marked for finalization */
		/* bit 7 is currently used by tests (luaL_checkmemory) */

        public static byte WHITEBITS = bit2mask (WHITE0BIT, WHITE1BIT);


		public static bool iswhite (GCObject x) { return testbits (x.marked, WHITEBITS); }
		public static bool isblack (GCObject x) { return testbit (x.marked, BLACKBIT); }
		/* neither white nor black */
		public static bool isgray (GCObject x) { return (testbits (x.marked, (byte)(WHITEBITS | (bitmask (BLACKBIT)))) == false); }

		public static bool tofinalize (GCObject x) { return testbit (x.marked, FINALIZEDBIT); }

		public static int otherwhite (global_State g) { return (g.currentwhite ^ WHITEBITS); }
		public static bool isdeadm (int ow, int m) { return (((m ^ WHITEBITS) & ow) == 0); }
		public static bool isdead (global_State g, GCObject x) { return isdeadm (otherwhite (g), x.marked); }

		public static void changewhite (GCObject x) { x.marked ^= WHITEBITS; }
		public static void gray2black (GCObject x) { l_setbit (ref x.marked, BLACKBIT); }
		
		public static byte luaC_white (global_State g) { return (byte)(g.currentwhite & WHITEBITS); }


        public static void luaC_checkGC (lua_State L) { 
			if (G (L).GCdebt > 0) {
				luaC_step (L);
			}
			condchangemem (L);
		}


        public static void luaC_barrier (lua_State L, GCObject p, TValue v) {
			if (iscollectable (v) && isblack (p) && iswhite (gcvalue (v)))
				lgc.luaC_barrier_ (L, p, gcvalue (v));
        }
        public static void luaC_barrier (lua_State L, GCObject p, int v) { luaC_barrier (L, p, L.stack[v]); }

        public static void luaC_barrierback (lua_State L, GCObject p, TValue v) {
			if (iscollectable (v) && isblack (p) && iswhite (gcvalue (v)))
                lgc.luaC_barrierback_ (L, gco2t (p));
		}
        public static void luaC_barrierback (lua_State L, GCObject p, int v) { luaC_barrierback (L, p, L.stack[v]); }

        public static void luaC_objbarrier (lua_State L, GCObject p, GCObject o) {
			if (isblack (p) && iswhite (o))
                lgc.luaC_barrier_ (L, obj2gco (p), obj2gco (o));
		}

        public static void luaC_upvalbarrier (lua_State L, UpVal uv) {
			if (iscollectable (uv.v) && upisopen (uv) == false)
                lgc.luaC_upvalbarrier_ (L, uv);
        }



		public static void luaC_fix (lua_State L, GCObject o) {
			global_State g = G (L);
			lua_assert (g.allgc == o);  /* object must be 1st in 'allgc' list! */
			lgc.white2gray (o);  /* they will be gray forever */
			g.allgc = o.next;  /* remove object from 'allgc' list */
			o.next = g.fixedgc;  /* link it to 'fixedgc' list */
			g.fixedgc = o;
		}


		/*
		** create a new collectable object (with given type and size) and link
		** it to 'allgc' list.
		*/
		public static T luaC_newobj<T> (lua_State L, int tt) where T : GCObject, new () {
			global_State g = G (L);
			T o = luaM_newobject<T> (L);
			o.marked = luaC_white (g);
			o.tt = (byte)tt;
			o.next = g.allgc;
			g.allgc = o;
			return o;
		}


        public static void luaC_upvdeccount (lua_State L, UpVal uv) {
            lua_assert (uv.refcount > 0);
            uv.refcount--;
            if (uv.refcount == 0 && upisopen (uv) == false)
                luaM_free (L, uv);
        }



        /*
        ** if object 'o' has a finalizer, remove it from 'allgc' list (must
        ** search the list to find it) and link it in 'finobj' list.
        */
        public static void luaC_checkfinalizer (lua_State L, GCObject o, Table mt) {
            global_State g = G (L);
            if (tofinalize (o) ||  /* obj. is already marked... */
                    gfasttm (g, mt, TMS.TM_GC) == null)  /* or has no finalizer? */
                return;  /* nothing to be done */
            else {  /* move 'o' to 'finobj' list */
                GCObject p;
                if (issweepphase (g)) {
                    lgc.makewhite (g, o);  /* "sweep" object 'o' */
                    long n = 0;
                    if (g.sweepgc == o.next)  /* should not remove 'sweepgc' object */
                        g.sweepgc = lgc.sweeptolive (L, g.sweepgc, ref n);  /* change 'sweepgc' */
                }
                /* search for pointer pointing to 'o' */
                for (p = g.allgc; p != o; p = p.next) { /* empty */ }
                p = o.next;  /* remove 'o' from 'allgc' list */
                o.next = g.finobj;  /* link it in 'finobj' list */
                g.finobj = o;
                l_setbit (ref o.marked, FINALIZEDBIT);  /* mark it as such */
            }
        }




		public static void luaC_freeallobjects (lua_State L) {
			global_State g = G (L);
            lgc.separatetobefnz (g, true);  /* separate all objects with finalizers */
			lua_assert (g.finobj == null);
            lgc.callallpendingfinalizers (L, 0);
			lua_assert (g.tobefnz == null);
			g.currentwhite = WHITEBITS;  /* this "white" makes all objects look dead */
			g.gckind = KGC_NORMAL;
            lgc.sweepwholelist (L, g.finobj);
            lgc.sweepwholelist (L, g.allgc);
            lgc.sweepwholelist (L, g.fixedgc);  /* collect fixed objects */
			lua_assert (g.strt.size == 0);
		}


		/*
		** advances the garbage collector until it reaches a state allowed
		** by 'statemask'
		*/
		public static void luaC_runtilstate (lua_State L, int statesmask) {
			global_State g = G (L);
			while (testbit ((byte)statesmask, g.gcstate) == false)
				lgc.singlestep (L);
		}


        /*
        ** performs a basic GC step when collector is running
        */
        public static void luaC_step (lua_State L) {
            global_State g = G (L);
            long debt = lgc.getdebt (g);  /* GC deficit (be paid now) */
            if (g.gcrunning == 0) {  /* not running? */
                luaE_setdebt (g, -GCSTEPSIZE * 10);  /* avoid being called too often */
                return;
            }
            do {  /* repeat until pause or enough "credit" (negative debt) */
                long work = lgc.singlestep (L);  /* perform one single step */
                debt -= work;
            } while (debt > -GCSTEPSIZE && g.gcstate != GCSpause);
            if (g.gcstate == GCSpause)
                lgc.setpause (g);  /* pause until next cycle */
            else {
                debt = (debt / g.gcstepmul) * lgc.STEPMULADJ;  /* convert 'work units' to Kb */
                luaE_setdebt (g, debt);
                lgc.runafewfinalizers (L);
            }
        }


		/*
		** Performs a full GC cycle; if 'isemergency', set a flag to avoid
		** some operations which could change the interpreter state in some
		** unexpected ways (running finalizers and shrinking some structures).
		** Before running the collection, check 'keepinvariant'; if it is true,
		** there may be some objects marked as black, so the collector has
		** to sweep all objects to turn them back to white (as white has not
		** changed, nothing will be collected).
		*/
		public static void luaC_fullgc (lua_State L, bool isemergency) {
			global_State g = G (L);
			lua_assert (g.gckind == KGC_NORMAL);
			if (isemergency) g.gckind = KGC_EMERGENCY;  /* set flag */
			if (keepinvariant (g)) {  /* black objects? */
                lgc.entersweep (L);  /* sweep everything to turn them back to white */
			}
			/* finish any pending sweep phase to start a new cycle */
			luaC_runtilstate (L, bitmask(GCSpause));
			luaC_runtilstate (L, ~bitmask(GCSpause));  /* start new collection */
			luaC_runtilstate (L, bitmask(GCScallfin));  /* run up to finalizers */
			/* estimate must be correct after a full GC cycle */
            lua_assert (g.GCestimate == gettotalbytes (g));
			luaC_runtilstate(L, bitmask(GCSpause));  /* finish collection */
			g.gckind = KGC_NORMAL;
            lgc.setpause (g);
		}

    }
}
