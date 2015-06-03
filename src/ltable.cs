using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	/*
	** Implementation of tables (aka arrays, objects, or hash tables).
	** Tables keep its elements in two parts: an array part and a hash part.
	** Non-negative integer keys are all candidates to be kept in the array
	** part. The actual size of the array is the largest 'n' such that at
	** least half the slots between 0 and n are in use.
	** Hash uses a mix of chained scatter table with Brent's variation.
	** A main invariant of these tables is that, if an element is not
	** in its main position (i.e. the 'original' position that its hash gives
	** to it), then the colliding element is in its own main position.
	** Hence even when the load factor reaches 100%, performance remains good.
	*/

    public static partial class imp {

        private static class ltable {

            /*
            ** Maximum size of array part (MAXASIZE) is 2^MAXABITS. MAXABITS is
            ** the largest integer such that MAXASIZE fits in an unsigned int.
            */
            public const int MAXABITS = sizeof (int) * CHAR_BIT - 1;
            public const long MAXASIZE = (1 << MAXABITS);

            /*
            ** Maximum size of hash part is 2^MAXHBITS. MAXHBITS is the largest
            ** integer such that 2^MAXHBITS fits in a signed int. (Note that the
            ** maximum number of elements in a table, 2^MAXABITS + 2^MAXHBITS, still
            ** fits comfortably in an unsigned int.)
            */
            public const int MAXHBITS = MAXABITS - 1;

            public static Node hashpow2 (Table t, long n) { return t.node[lmod (n, sizenode (t))]; }

            public static Node hashstr (Table t, TString str) { return hashpow2 (t, str.hash); }
            public static Node hashboolean (Table t, long p) { return hashpow2 (t, p); }
            public static Node hashint (Table t, long i) { return hashpow2 (t, i); }

            /*
            ** for some types, it is better to avoid modulus by power of 2, as
            ** they tend to have many 2 factors.
            */
            public static Node hashmod (Table t, long n) { return t.node[(n % ((sizenode (t) - 1) | 1))]; }

            public static Node hashpointer (Table t, object p) { return hashmod (t, point2int (p)); }

			public static Node[] dummynode = { luaM_newobject<Node> (null) };

            public static bool isdummy (Node n) { return (n == dummynode[0]); }
            public static bool isdummy (Node[] n) { return (n == dummynode); }

            /*
            ** Checks whether a float has a value representable as a lua_Integer
            ** (and does the conversion if so)
            */
            public static bool numisinteger (double x, ref long p) {
                if (x == l_floor (x))  /* integral value? */
                    return lua530.lua_numbertointeger (x, ref p);  /* try as an integer */
                else return false;
            }

            public static double frexp (double d, ref int n) {
                double mantissa = d;
                int exponent = 0;
                int sign = 1;

                if (mantissa < 0) {
                    sign--;
                    mantissa = -mantissa;
                }

                while (mantissa < 0.5) {
                    mantissa *= 2;
                    exponent--;
                }

                while (mantissa >= 1) {
                    mantissa *= 0.5;
                    exponent++;
                }

                mantissa *= sign;
                n = exponent;
                return mantissa;
            }

            /*
            ** hash for floating-point numbers
            */
            public static Node hashfloat (Table t, double n) {
                int i = 0;
                n = ltable.frexp (n, ref i) * (MAX_INT - DBL_MAX_EXP);
                i += (int)n;
                if (i < 0) {
                    if ((uint)i == ((0u - i)))  /* use unsigned to avoid overflows */
                        i = 0;  /* handle INT_MIN */
                    i = -i;  /* must be a positive value */
                }
                return hashmod (t, i);
            }



            /*
            ** returns the 'main' position of an element in a table (that is, the index
            ** of its hash value)
            */
            public static Node mainposition (Table t, TValue key) {
                switch (ttype (key)) {
                    case LUA_TNUMINT:
                        return hashint (t, ivalue (key));
                    case LUA_TNUMFLT:
                        return hashfloat (t, fltvalue (key));
                    case LUA_TSHRSTR:
                        return hashstr (t, tsvalue (key));
                    case LUA_TLNGSTR: {
                        TString s = tsvalue (key);
                        if (s.extra == 0) {  /* no hash? */
                            s.hash = luaS_hash (s.data, s.data.Length, (uint)s.hash);
                            s.extra = 1;
                        }
                        return hashstr (t, tsvalue (key));
                    }
                    case lua530.LUA_TBOOLEAN:
                        return hashboolean (t, bvalue (key));
                    case lua530.LUA_TLIGHTUSERDATA:
                        return hashpointer (t, pvalue (key));
                    case LUA_TLCF:
					return hashpointer (t, key); /* fvalue (key) */
                    default:
                        return hashpointer (t, gcvalue (key));
                }
			}


			/*
            ** returns the index for 'key' if 'key' is an appropriate key to live in
            ** the array part of the table, 0 otherwise.
            */
			public static long arrayindex (TValue key) {
				if (ttisinteger (key)) {
					long k = ivalue (key);
					if (0 < k && k <= MAXASIZE)
						return k;  /* 'key' is an appropriate array index */
				}
				return 0;  /* 'key' did not match some condition */
			}

			
			/*
            ** returns the index of a 'key' for table traversals. First goes all
            ** elements in the array part, then elements in the hash part. The
            ** beginning of a traversal is signaled by 0.
            */
			public static long findindex (lua_State L, Table t, TValue key) {
				if (ttisnil (key)) return 0;  /* first iteration */
				long i = arrayindex (key);
				if (i != 0 && i <= t.sizearray)  /* is 'key' inside array part? */
					return i;  /* yes; that's the index */
				else {
					Node n = mainposition (t, key);
					for (; ; ) {  /* check whether 'key' is somewhere in the chain */
						/* key may be dead already, but it is ok to use it in 'next' */
						if (luaV_rawequalobj (n.i_key.tvk, key) ||
						    (ttisdeadkey (n.i_key.tvk) && iscollectable (key) && (deadvalue (n.i_key.tvk) == gcvalue (key)))) {
							i = n.index;  /* key index in hash table */
							/* hash elements are numbered after array ones */
							return (i + 1) + t.sizearray;
						}
						
						n = n.i_key.next;
						if (n == null)
							luaG_runerror (L, "invalid key to 'next'");  /* key not found */
					}
				}
			}


			/*
			** {=============================================================
			** Rehash
			** ==============================================================
			*/
			
			
			/*
            ** Compute the optimal size for the array part of table 't'. 'nums' is a
            ** "count array" where 'nums[i]' is the number of integers in the table
            ** between 2^(i - 1) + 1 and 2^i. Put in '*narray' the optimal size, and
            ** return the number of elements that will go to that part.
            */
			public static long computesizes (long[] nums, ref long narray) {
				long i = 0;
				long twotoi = 1;  /* 2^i */
				long a = 0;  /* number of elements smaller than 2^i */
				long na = 0;  /* number of elements to go to array part */
				long n = 0;  /* optimal size for array part */
				for (; (long)(twotoi / 2) < narray; twotoi *= 2) {
					if (nums[i] > 0) {
						a += nums[i];
						if (a > (long)(twotoi / 2)) {  /* more than half elements present? */
							n = twotoi;  /* optimal size (till now) */
							na = a;  /* all elements up to 'n' will go to array part */
						}
					}
					if (a == narray) break;  /* all elements already counted */
					i++;
				}
				narray = n;
				lua_assert ((long)(narray / 2) <= na && na <= narray);
				return na;
			}


			public static long countint (TValue key, long[] nums) {
				long k = arrayindex (key);
				if (k != 0) {  /* is 'key' an appropriate array index? */
					nums[luaO_ceillog2 (k)]++;  /* count as such */
					return 1;
				}
				else
					return 0;
			}
			
			public static long numusearray (Table t, long[] nums) {
				long ttlg = 1;  /* 2^lg */
				long ause = 0;  /* summation of 'nums' */
				long i = 1;  /* count to traverse all array keys */
				/* traverse each slice */
				for (int lg = 0; lg <= MAXABITS; lg++) {
					long lc = 0;  /* counter */
					long lim = ttlg;
					if (lim > t.sizearray) {
						lim = t.sizearray;  /* adjust upper limit */
						if (i > lim)
							break;  /* no more elements to count */
					}
					/* count elements in range (2^(lg - 1), 2^lg] */
					for (; i <= lim; i++) {
						if (ttisnil (t.array[i - 1]) == false)
							lc++;
					}
					nums[lg] += lc;
					ause += lc;
					ttlg *= 2;
				}
				return ause;
			}


			public static long numusehash (Table t, long[] nums, ref long pnasize) {
				long totaluse = 0;  /* total number of elements */
				long ause = 0;  /* elements added to 'nums' (can go to array part) */
				long i = sizenode (t);
				while (i-- > 0) {
					Node n = t.node[i];
					if (ttisnil (n.i_val) == false) {
						ause += countint (n.i_key.tvk, nums);
						totaluse++;
					}
				}
				pnasize += ause;
				return totaluse;
			}

			
			public static void setarrayvector (lua_State L, Table t, long size) {
				luaM_reallocvector<TValue> (L, ref t.array, t.sizearray, size);
				for (long i = t.sizearray; i < size; i++) {
					TValue o = luaM_newobject<TValue> (L);
					setnilvalue (o);
					t.array[i] = o;
				}
				t.sizearray = size;
			}


			public static void setnodevector (lua_State L, Table t, long size) {
				int lsize;
				if (size == 0) {  /* no elements to hash part? */
					t.node = dummynode;  /* use common 'dummynode' */
					lsize = 0;
				}
				else {
					lsize = luaO_ceillog2 (size);
					if (lsize > MAXHBITS)
						luaG_runerror (L, "table overflow");
					size = twoto (lsize);
					t.node = luaM_fullvector<Node> (L, size);
					for (long i = 0; i < size; i++) {
						Node n = t.node[i];
						n.index = i;
						n.i_key.next = null;
						setnilvalue (n.i_key.tvk);
						setnilvalue (n.i_val);
					}
				}
				t.lsizenode = (byte)lsize;
				t.lastfree = size;  /* all positions are free */
			}


			/*
            ** nums[i] = number of keys 'k' where 2^(i - 1) < k <= 2^i
            */
			public static void rehash (lua_State L, Table t, TValue ek) {
				long[] nums = luaM_emptyvector<long> (L, MAXABITS + 1);
				for (int i = 0; i <= MAXABITS; i++) nums[i] = 0;  /* reset counts */
				long nasize = numusearray (t, nums);  /* count keys in array part */
				long totaluse = nasize;  /* all those keys are integer keys */
				totaluse += numusehash (t, nums, ref nasize);  /* count keys in hash part */
				/* count extra key */
				nasize += countint (ek, nums);
				totaluse++;
				/* compute new size for array part */
				long na = computesizes (nums, ref nasize);
				/* resize the table to new computed sizes */
				luaH_resize (L, t, nasize, totaluse - na);
			}


            public static Node getfreepos (Table t) {
                while (t.lastfree > 0) {
                    t.lastfree--;
                    if (ttisnil (t.node[t.lastfree].i_key.tvk))
                        return t.node[t.lastfree];
                }
                return null;  /* could not find a free place */
			}


			public static void nodecopy (Node n1, Node n2) {
				n1.i_key.next = n2.i_key.next;
				n1.i_key.tvk = n2.i_key.tvk;
				n1.i_val = n2.i_val;
			}


			public static int unbound_search (Table t, long j) {
				long i = j;  /* i is zero or a present index */
				j++;
				/* find 'i' and 'j' such that i is present and j is not */
				while (ttisnil (luaH_getint (t, j)) == false) {
					i = j;
					if (j > (MAX_INT / 2)) {  /* overflow? */
						/* table was built with bad purposes: resort to linear search */
						i = 1;
						while (ttisnil (luaH_getint (t, i)) == false) i++;
                        return (int)(i - 1);
					}
					j *= 2;
				}
				/* now do a binary search between them */
				while (j - i > 1) {
					long m = (i + j) / 2;
					if (ttisnil (t.array[m - 1])) j = m;
					else i = m;
				}
                return (int)i;
			}
        }


        public const int CHAR_BIT = 8;


        public static void invalidateTMcache (Table t) { t.flags = 0; }


        public static int luaH_next (lua_State L, Table t, int stkid) {
            TValue key = L.stack[stkid];
            long i = ltable.findindex (L, t, key);  /* find original element */	
            for (; i < t.sizearray; i++) {  /* try first array part */
                if (ttisnil (t.array[i]) == false) {  /* a non-nil value? */
                    setivalue (key, i + 1);
                    setobj2s (L, L.stack[stkid + 1], t.array[i]);
                    return 1;
                }
            }
            for (i -= t.sizearray; i < sizenode (t); i++) {  /* hash part */
                if (ttisnil (t.node[i].i_val) == false) {  /* a non-nil value? */
                    setobj2s (L, L.stack[stkid], t.node[i].i_key.tvk);
                    setobj2s (L, L.stack[stkid + 1], t.node[i].i_val);
                    return 1;
                }
            }
            return 0;  /* no more elements */
        }


        public static void luaH_resize (lua_State L, Table t, long nasize, long nhsize) {
            long oldasize = t.sizearray;
            long oldhsize = t.lsizenode;
			Node[] nold = t.node;  /* save old hash ... */
			if (nasize > oldasize)  /* array part must grow? */
				ltable.setarrayvector (L, t, nasize);
			/* create new hash part with appropriate size */
			ltable.setnodevector (L, t, nhsize);
			if (nasize < oldasize) {  /* array part must shrink? */
				t.sizearray = nasize;
				/* re-insert elements from vanishing slice */
                for (long i = nasize; i < oldasize; i++) {
					if (ttisnil (t.array[i]) == false)
						luaH_setint (L, t, i + 1, t.array[i]);
				}
				/* shrink array */
				luaM_reallocvector<TValue> (L, ref t.array, oldasize, nasize);
			}
			/* re-insert elements from hash part */
            for (long j = twoto ((int)oldhsize) - 1; j >= 0; j--) {
				Node old = nold[j];
				if (ttisnil (old.i_val) == false) {
					/* doesn't need barrier/invalidate cache, as entry was
         				already present in the table */
					setobjt2t (L, luaH_set (L, t, old.i_key.tvk), old.i_val);
				}
			}
            if (ltable.isdummy (nold) == false)
                luaM_freearray (L, nold);  /* free old array */
        }


        public static void luaH_resizearray (lua_State L, Table t, long nasize) {
            long nsize = ltable.isdummy (t.node) ? 0 : sizenode (t);
            luaH_resize (L, t, nasize, nsize);
        }


        public static Table luaH_new (lua_State L) {
            Table t = luaC_newobj<Table> (L, lua530.LUA_TTABLE);
            t.metatable = null;
            t.flags = unchecked ((byte)(~0u));
            t.array = null;
            t.sizearray = 0;
            ltable.setnodevector (L, t, 0);
            return t;
        }


        public static void luaH_free (lua_State L, Table t) {
            if (ltable.isdummy (t.node) == false)
                luaM_freearray (L, t.node);
            luaM_freearray (L, t.array);
            luaM_free (L, t);
        }


        /*
        ** inserts a new key into a hash table; first, check whether key's main
        ** position is free. If not, check whether colliding node is in its main
        ** position or not: if it is not, move colliding node to an empty place and
        ** put new key in its main position; otherwise (colliding node is in its main
        ** position), new key goes to an empty position.
        */
        public static TValue luaH_newkey (lua_State L, Table t, TValue key) {
            TValue aux;
            if (ttisnil (key)) luaG_runerror (L, "table index is nil");
            else if (ttisfloat (key)) {
                double n = fltvalue (key);
                long k = 0;
                if (luai_numisnan (n))
                    luaG_runerror (L, "table index is NaN");
                if (ltable.numisinteger (n, ref k)) {  /* index is int? */
					aux = luaM_newobject<TValue> (L);
                    setivalue (aux, k);
                    key = aux;  /* insert it as an integer */
                }
            }
            Node mp = ltable.mainposition (t, key);
            if ((ttisnil (mp.i_val) == false) || ltable.isdummy (mp)) {  /* main position is taken? */
                Node f = ltable.getfreepos (t);  /* get a free place */
                if (f == null) {  /* cannot find a free place? */
                    ltable.rehash (L, t, key);  /* grow table */
                    /* whatever called 'newkey' takes care of TM cache and GC barrier */
                    return luaH_set (L, t, key);  /* insert key into grown table */
                }
                lua_assert (ltable.isdummy (f) == false);
                Node othern = ltable.mainposition (t, mp.i_key.tvk);
                if (othern != mp) {  /* is colliding node out of its main position? */
                    /* yes; move colliding node into free position */
					while (othern.i_key.next != mp)  /* find previous */
						othern = othern.i_key.next;
					othern.i_key.next = f;
					ltable.nodecopy (f, mp);  /* copy colliding node into free pos. (mp->next also goes) */
					
					setnilvalue (mp.i_val);
					setnilvalue (mp.i_key.tvk);
					mp.i_key.next = null;

                }
                else {  /* colliding node is in its own main position */
                    /* new node will go into free position */
                    if (mp.i_key.next != null)
                        f.i_key.next = mp.i_key.next;
                    else
                        lua_assert (f.i_key.next == null);
                    mp.i_key.next = f;
                    mp = f;
                }
            }
            setnodekey (L, mp.i_key, key);
			luaC_barrierback (L, t, key);
            lua_assert (ttisnil (mp.i_val));
            return mp.i_val;
        }


        /*
        ** search function for integers
        */
        public static TValue luaH_getint (Table t, long key) {
            /* (1 <= key && key <= t->sizearray) */
            if ((ulong)(key - 1) < (ulong)t.sizearray)
                return t.array[key - 1];
            else {
                Node n = ltable.hashint (t, key);
                for (; ; ) {  /* check whether 'key' is somewhere in the chain */
                    if (ttisinteger (n.i_key.tvk) && ivalue (n.i_key.tvk) == key)
                        return n.i_val;  /* that's it */
                    else {
                        n = n.i_key.next;
                        if (n == null) break;
                    }
                }
            }
            return luaO_nilobject;
        }


        /*
        ** search function for short strings
        */
        public static TValue luaH_getstr (Table t, TString key) {
            Node n = ltable.hashstr (t, key);
            lua_assert (key.tt == LUA_TSHRSTR);
            for (; ; ) {  /* check whether 'key' is somewhere in the chain */
                TValue k = n.i_key.tvk;
                if (ttisshrstring (k) && eqshrstr (tsvalue (k), key))
                    return n.i_val;  /* that's it */
                else {
                    n = n.i_key.next;
                    if (n == null) break;
                }
            }
            return luaO_nilobject;
        }


        /*
        ** main search function
        */
        public static TValue luaH_get (Table t, TValue key) {
            switch (ttype (key)) {
                case LUA_TSHRSTR: return luaH_getstr (t, tsvalue (key));
                case LUA_TNUMINT: return luaH_getint (t, ivalue (key));
                case lua530.LUA_TNIL: return luaO_nilobject;
                case LUA_TNUMFLT: {
                    long k = 0;
                    if (ltable.numisinteger (fltvalue (key), ref k))  /* index is int? */
                        return luaH_getint (t, k);  /* use specialized version */
                    break;
                }
            }

            Node n = ltable.mainposition (t, key);
            for (; ; ) {  /* check whether 'key' is somewhere in the chain */
                if (luaV_rawequalobj (n.i_key.tvk, key))
                    return n.i_val;  /* that's it */
                else {
                    n = n.i_key.next;
                    if (n == null) break;
                }
            }
            return luaO_nilobject;
        }


        /*
        ** beware: when using this function you probably need to check a GC
        ** barrier and invalidate the TM cache.
        */

        public static TValue luaH_set (lua_State L, Table t, TValue key) {
            TValue p = luaH_get (t, key);
            if (p != luaO_nilobject)
                return p;
            else return luaH_newkey (L, t, key);
        }


        public static void luaH_setint (lua_State L, Table t, long key, TValue value) {
            TValue p = luaH_getint (t, key);
            if (p == luaO_nilobject) {
                TValue k = luaM_newobject<TValue> (L);
                setivalue (k, key);
                p = luaH_newkey (L, t, k);
            }
            setobj2t (L, p, value);
        }


		/*
		** Try to find a boundary in table 't'. A 'boundary' is an integer index
		** such that t[i] is non-nil and t[i+1] is nil (and 0 if t[1] is nil).
		*/
		public static int luaH_getn (Table t) {
			long j = t.sizearray;
			if (j > 0 && ttisnil (t.array[j - 1])) {
				/* there is a boundary in the array part: (binary) search for it */
				long i = 0;
				while (j - i > 1) {
					long m = (i + j) / 2;
					if (ttisnil (t.array[m - 1])) j = m;
					else i = m;
				}
                return (int)i;
			}
			/* else must find a boundary in hash part */
			else if (ltable.isdummy (t.node))  /* hash part is empty? */
				return (int)j;  /* that is easy... */
			else return ltable.unbound_search (t, j);
		}
    }
}
