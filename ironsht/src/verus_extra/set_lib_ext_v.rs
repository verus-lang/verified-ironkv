
use vstd::prelude::*;
use vstd::seq_lib::*;
use vstd::set_lib::*;

verus! {

pub proof fn lemma_flatten_sets_union<A>(sets1: Set<Set<A>>, sets2: Set<Set<A>>)
    ensures
        sets1.union(sets2).flatten() == sets1.flatten().union(sets2.flatten())
    decreases
        sets1.len(),
{
    let s1 = sets1.union(sets2).flatten();
    let s2 = sets1.flatten().union(sets2.flatten());
    assert forall|a: A| s2.contains(a) implies s1.contains(a) by {
        if sets1.flatten().contains(a) {
            let s = choose|s| #[trigger] sets1.contains(s) && s.contains(a);
            assert(sets1.union(sets2).contains(s));
            assert(s1.contains(a));
        }
        else {
            assert(sets2.flatten().contains(a));
            let s = choose|s| #[trigger] sets2.contains(s) && s.contains(a);
            assert(sets1.union(sets2).contains(s));
            assert(s1.contains(a));
        }
    }
}

pub proof fn lemma_flatten_sets_union_auto<A>()
    ensures forall |sets1: Set<Set<A>>, sets2: Set<Set<A>>|
        #[trigger] sets1.union(sets2).flatten() == sets1.flatten().union(sets2.flatten())
{
    assert forall |sets1: Set<Set<A>>, sets2: Set<Set<A>>|
        #[trigger] sets1.union(sets2).flatten() == sets1.flatten().union(sets2.flatten()) by {
        lemma_flatten_sets_union(sets1, sets2);
    }
}

pub proof fn set_map_union<A, B>(s1: Set<A>, s2: Set<A>, f: spec_fn(A) -> B)
    ensures (s1 + s2).map(f) == s1.map(f) + s2.map(f)
{
    assert_sets_equal!((s1 + s2).map(f) == s1.map(f) + s2.map(f), y => {
        if s1.map(f).contains(y) {
            let x = choose |x| s1.contains(x) && f(x) == y;
            assert((s1 + s2).contains(x));
        } else if s2.map(f).contains(y) {
            let x = choose |x| s2.contains(x) && f(x) == y;
            assert((s1 + s2).contains(x));
        }
    });
}

pub proof fn set_map_union_auto<A, B>()
    ensures forall |s1: Set<A>, s2: Set<A>, f: spec_fn(A) -> B|
        #[trigger] (s1 + s2).map(f) == s1.map(f) + s2.map(f)
{
    assert forall |s1: Set<A>, s2: Set<A>, f: spec_fn(A) -> B|
        #[trigger] ((s1 + s2).map(f)) == s1.map(f) + s2.map(f) by {
        set_map_union(s1, s2, f);
    }
}

pub proof fn seq_map_values_concat<A, B>(s1: Seq<A>, s2: Seq<A>, f: spec_fn(A) -> B)
    ensures (s1 + s2).map_values(f) == s1.map_values(f) + s2.map_values(f)
{
    assert_seqs_equal!((s1 + s2).map_values(f) == s1.map_values(f) + s2.map_values(f), i => {
        if i < s1.len() {
            assert((s1+s2)[i] == s1[i]);
        } else {
            assert((s1+s2)[i] == s2[i - s1.len()]);
        }
    });
}

pub proof fn seq_map_values_concat_auto<A, B>()
ensures forall |s1: Seq<A>, s2: Seq<A>, f: spec_fn(A) -> B|
    #[trigger] (s1 + s2).map_values(f) == s1.map_values(f) + s2.map_values(f)
{
    assert forall |s1: Seq<A>, s2: Seq<A>, f: spec_fn(A) -> B|
        #[trigger] ((s1 + s2).map_values(f)) == s1.map_values(f) + s2.map_values(f) by {
        seq_map_values_concat(s1, s2, f);
    }
}

pub open spec fn flatten_set_seq<A>(sets: Seq<Set<A>>) -> Set<A>
{
    sets.fold_left(Set::<A>::empty(), |s1: Set<A>, s2: Set<A>| s1.union(s2))
}

pub proof fn lemma_flatten_set_seq_spec<A>(sets: Seq<Set<A>>)
    ensures
        (forall |x:A| #[trigger] flatten_set_seq(sets).contains(x) ==>
            exists |i: int| 0 <= i < sets.len() && #[trigger] sets[i].contains(x)),
        (forall |x:A, i:int| 0 <= i < sets.len() && #[trigger] sets[i].contains(x) ==>
            flatten_set_seq(sets).contains(x))
    decreases sets.len()
{
    if sets.len() == 0 {
    } else {
        lemma_flatten_set_seq_spec(sets.drop_last());
        assert forall |x:A| flatten_set_seq(sets).contains(x) implies
            exists |i: int| 0 <= i < sets.len() && #[trigger] sets[i].contains(x) by {
            if sets.last().contains(x) {
            } else {
                assert(flatten_set_seq(sets.drop_last()).contains(x));
            }
        }
        assert forall |x:A, i:int| 0 <= i < sets.len() && #[trigger] sets[i].contains(x) implies
            flatten_set_seq(sets).contains(x) by {
            if i == sets.len() - 1 {
                assert(sets.last().contains(x));
                assert(flatten_set_seq(sets) == flatten_set_seq(sets.drop_last()).union(sets.last()));
            } else {
                assert(0 <= i < sets.drop_last().len() && sets.drop_last()[i].contains(x));
            }
        }
    }
}


pub proof fn lemma_seq_push_to_set<A>(s: Seq<A>, x: A)
    ensures s.push(x).to_set() == s.to_set().insert(x)
{
    assert_sets_equal!(s.push(x).to_set() == s.to_set().insert(x), elem => {
        if elem == x {
            assert(s.push(x)[s.len() as int] == x);
            assert(s.push(x).contains(x))
        } else {
            if s.to_set().insert(x).contains(elem) {
                assert(s.to_set().contains(elem));
                let i = choose |i: int| 0 <= i < s.len() && s[i] == elem;
                assert(s.push(x)[i] == elem);
            }
        }
    });
}

pub proof fn lemma_set_map_insert<A, B>(s: Set<A>, f: spec_fn(A) -> B, x: A)
    ensures s.insert(x).map(f) == s.map(f).insert(f(x))
{
    assert_sets_equal!(s.insert(x).map(f) == s.map(f).insert(f(x)), y => {
        if y == f(x) {
            assert(s.insert(x).contains(x)); // OBSERVE
            // assert(s.map(f).insert(f(x)).contains(f(x)));
        } else {
            if s.insert(x).map(f).contains(y) {
                let x0 = choose |x0| s.contains(x0) && y == f(x0);
                assert(s.map(f).contains(y));
            } else {
                if s.map(f).insert(f(x)).contains(y) {
                    let x0 = choose |x0| s.contains(x0) && y == f(x0);
                    assert(s.map(f).contains(y));
                    assert(s.insert(x).contains(x0));
                }
            }
        }
    });
}

// TODO(verus): This consequence should somehow be broadcast from map_values/map
pub proof fn lemma_seq_map_equiv<A, B>(f: spec_fn(A) -> B, g: spec_fn(int, A) -> B)
requires
    forall |i: int, a: A| #[trigger] g(i, a) == f(a)
ensures
    forall |s: Seq<A>| s.map_values(f) == s.map(g)
{
    assert forall |s: Seq<A>| s.map_values(f) == s.map(g) by {
        assert_seqs_equal!(s.map_values(f), s.map(g));
    }
}

pub proof fn lemma_to_set_distributes_over_addition<A>(s: Seq<A>, t: Seq<A>)
ensures (s+t).to_set() == s.to_set() + t.to_set()
{
    let left = (s+t).to_set();
    let right = s.to_set() + t.to_set();
    assert forall |x| right.contains(x) implies left.contains(x) by {
        assert(s.to_set()+t.to_set() == s.to_set().union(t.to_set()));
        if s.to_set().contains(x) {
            let si = choose |si| 0<=si<s.len() && s[si] == x;
            assert((s+t)[si] == x);
        } else {
            let ti = choose |ti| 0<=ti<t.len() && t[ti] == x;
            assert((s+t)[s.len() + ti] == x);
        }
    }
    assert_sets_equal!(left, right);
}


pub proof fn lemma_to_set_union_auto<A>()
    ensures forall |s: Seq<A>, t: Seq<A>| #[trigger] (s+t).to_set() == s.to_set() + t.to_set()
{
    assert forall |s: Seq<A>, t: Seq<A>| #[trigger] (s+t).to_set() == s.to_set() + t.to_set() by {
        lemma_to_set_distributes_over_addition(s, t);
    }
}

pub proof fn lemma_to_set_singleton_auto<A>()
ensures
    forall |x: A| #[trigger] seq![x].to_set() == set![x],
{
    assert forall |x: A| #[trigger] seq![x].to_set() =~= set![x] by {
        assert(seq![x][0] == x);
    }
}

pub proof fn lemma_map_values_singleton_auto<A, B>()
ensures
    forall |x: A, f: spec_fn(A) -> B| #[trigger] seq![x].map_values(f) =~= seq![f(x)],
{
}

pub proof fn lemma_map_set_singleton_auto<A, B>()
ensures
    forall |x: A, f: spec_fn(A) -> B| #[trigger] set![x].map(f) == set![f(x)],
{
    assert forall |x: A, f: spec_fn(A) -> B| #[trigger] set![x].map(f) =~= set![f(x)] by {
        assert(set![x].contains(x));
    }
}

pub proof fn lemma_map_seq_singleton_auto<A, B>()
ensures
    forall |x: A, f: spec_fn(A) -> B| #[trigger] seq![x].map_values(f) =~= seq![f(x)],
{
}


pub proof fn flatten_sets_singleton_auto<A>()
ensures
    forall |x: Set<A>| #[trigger] (set![x]).flatten() == x,
{
    assert forall |x: Set<A>| #[trigger] (set![x]).flatten() == x by {
        let x_alt = set![x].flatten();
        assert forall|a: A| x.contains(a) implies x_alt.contains(a) by {
            assert(set![x].contains(x));
        }
    }
}

// TODO(Tej): We strongly suspect there is a trigger loop in these auto
// lemmas somewhere, but it's not easy to see from the profiler yet.

}
