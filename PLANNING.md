
2022.01.10 meeting with Jon, Bryan

Do the SHT/KV to start

* port the distributed system's host model as trusted top spec
* Rules of the port
    * Keep the same top-level and bottom-level specs
    * Make the same algorithmic decisions
    * Otherwise, code/proof clean-up is okay, especially if it demonstrates Verus' capabilities
        * e.g. use separation logic-y statements for simpler VCs in the marshalling code
        * e.g. linear state machine for managing IO mover compatibility requirement
* Evaluation
    * Show comparable (or better!) performance to show that our implementation didn't take shortcuts on perf
        * Hence we should probably verify the marshalling, since it's important (a big part of) performance
    * Measure proof overhead
    * Measure proof verification time
    * Tell stories about the many ways things are cleaner, faster better

# jonh notes
clear ; unbuffer ../../../verus-rust/source/tools/rust-verify.sh --expand-errors --triggers-silent --time --no-lifetime src/lib.rs 2>&1 | less -R
