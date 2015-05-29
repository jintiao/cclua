using System;

namespace cclua53
{

    public static partial class imp {

        private static class ltable {

            /*
            ** Maximum size of array part (MAXASIZE) is 2^MAXABITS. MAXABITS is
            ** the largest integer such that MAXASIZE fits in an unsigned int.
            */
            public const int CHAR_BIT = 8;
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

            public static Node[] dummynode = { new Node () };

            public static bool isdummy (Node n) {
                return (n == dummynode[0]);
            }

            public static bool isdummy (Node[] n) {
                return (n == dummynode);
            }

            /*
            ** Checks whether a float has a value representable as a lua_Integer
            ** (and does the conversion if so)
            */
            public static bool numisinteger (double x, ref long p) {
                if (x == l_floor (x))  /* integral value? */
                    return (cclua.lua_numbertointeger (x, ref p) != 0);  /* try as an integer */
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


            public static void setnodevector (cclua.lua_State L, Table t, long size) {
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

            public static void setarrayvector (cclua.lua_State L, Table t, long size) {
				luaM_reallocvector<TValue> (L, ref t.array, t.sizearray, size);
                for (long i = t.sizearray; i < size; i++) {
                    TValue o = luaM_newobject<TValue> ();
                    setnilvalue (o);
                    t.array[i] = o;
                }
				t.sizearray = size;
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
                    case cclua.LUA_TBOOLEAN:
                        return hashboolean (t, bvalue (key));
                    case cclua.LUA_TLIGHTUSERDATA:
                        return hashpointer (t, pvalue (key));
                    //case LUA_TLCF:
                    //    return hashpointer (t, fvalue (key));
                    default:
                        return hashpointer (t, gcvalue (key));
                }
            }

            public static Node getfreepos (Table t) {
                while (t.lastfree > 0) {
                    t.lastfree--;
                    if (ttisnil (t.node[t.lastfree].i_key.tvk))
                        return t.node[t.lastfree];
                }
                return null;  /* could not find a free place */
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

            public static long countint (TValue key, long[] nums) {
                long k = arrayindex (key);
                if (k != 0) {  /* is 'key' an appropriate array index? */
                    nums[luaO_ceillog2 (k)]++;  /* count as such */
                    return 1;
                }
                else
                    return 0;
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

            /*
            ** nums[i] = number of keys 'k' where 2^(i - 1) < k <= 2^i
            */
            public static void rehash (cclua.lua_State L, Table t, TValue ek) {
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
            public static long findindex (cclua.lua_State L, Table t, TValue key) {
                if (ttisnil (key)) return 0;  /* first iteration */
                long i = arrayindex (key);
                if (i != 0 && i <= t.sizearray)  /* is 'key' inside array part? */
                    return i;  /* yes; that's the index */
                else {
                    Node n = mainposition (t, key);
                    for (; ; ) {  /* check whether 'key' is somewhere in the chain */
                        /* key may be dead already, but it is ok to use it in 'next' */
                        if (luaV_rawequalobj (n.i_key.tvk, key) != 0) {
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
        }




        public static int luaH_next (cclua.lua_State L, Table t, int stkid) {
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

        public static void luaH_resize (cclua.lua_State L, Table t, long nasize, long nhsize) {
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

        public static void luaH_resizearray (cclua.lua_State L, Table t, long nasize) {
            long nsize = ltable.isdummy (t.node) ? 0 : sizenode (t);
            luaH_resize (L, t, nasize, nsize);
        }

        public static Table luaH_new (cclua.lua_State L) {
            Table t = luaC_newobj<Table> (L, cclua.LUA_TTABLE);
            t.metatable = null;
            t.flags = unchecked ((byte)(~0u));
            t.array = null;
            t.sizearray = 0;
            ltable.setnodevector (L, t, 0);
            return t;
        }

        public static void luaH_free (cclua.lua_State L, Table t) {
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
        public static TValue luaH_newkey (cclua.lua_State L, Table t, TValue key) {
            TValue aux;
            if (ttisnil (key)) luaG_runerror (L, "table index is nil");
            else if (ttisfloat (key)) {
                double n = fltvalue (key);
                long k = 0;
                if (luai_numisnan (n) != 0)
                    luaG_runerror (L, "table index is NaN");
                if (ltable.numisinteger (n, ref k)) {  /* index is int? */
                    aux = new TValue ();
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
                case cclua.LUA_TNIL: return luaO_nilobject;
                case LUA_TNUMFLT: {
                    long k;
                    if (ltable.numisinteger (fltvalue (key), ref k))  /* index is int? */
                        return luaH_getint (t, k);  /* use specialized version */
                    break;
                }
            }

            Node n = ltable.mainposition (t, key);
            for (; ; ) {  /* check whether 'key' is somewhere in the chain */
                if (luaV_rawequalobj (n.i_key.tvk, key) != 0)
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

        public static TValue luaH_set (cclua.lua_State L, Table t, TValue key) {
            TValue p = luaH_get (t, key);
            if (p != luaO_nilobject)
                return p;
            else return luaH_newkey (L, t, key);
        }

        public static void luaH_setint (cclua.lua_State L, Table t, long key, TValue value) {
            TValue p = luaH_getint (t, key);
            if (p == luaO_nilobject) {
                TValue k = luaM_newobject<TValue> ();
                setivalue (k, key);
                p = luaH_newkey (L, t, k);
            }
            setobj2t (L, p, value);
        }
    }
}
