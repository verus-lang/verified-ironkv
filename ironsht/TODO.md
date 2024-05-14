# How to build

```
cd ironfleet-comparison #(one leven above here)
unbuffer scons --verus-path=/home/jonh/verus | less -R
```

# Known Missing

* Missing `host_impl_v` implementations (lorch, jonh)
  * `extract_range_impl`

# Done

✓ Change NodeIdentity -> AbstractEndPoint = Seq<u8>

✓ How is it that EndPoint = Vec<u8> is in spec places?

✓ `hashmap_t`: axiomatize Rust HashMap? (Tej)

✓ `cmessage_v::CPacket` (Tej)

✓ `args_t::clone_vec` axiomatize? (Tej)

✓ Write missing `host_protocol_t` transitions (lorch)

✓ Plug `io_t` into C# hooks (lorch)

✓ Marshalling library for arbitrary Rust `struct`s and `enum`s (jayb)
