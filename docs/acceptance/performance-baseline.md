# Acceptance performance evidence — initial measurement protocol

This is a new document corpus with explicit M12 engine provenance. The existing pinned M8 resource harness and its inputs/results remain unchanged. The two experiments are never relabelled as each other.

For B00, B01 and B02-100 collect two batches, each with 10 independent production-CLI cold processes and 30 same-process warm conversions after 3 unmeasured warmups. Raw JSON records each input/source/environment/font fingerprint, PDF hash/bytes/pages, sample index and measurement scope. Missing/negative/noninteger allocation, nonfinite time, incorrect count/order, reused cold process IDs or inconsistent output fails validation. Summaries recompute medians from raw samples.

- **Cold elapsed:** from spawning the production CLI until it exits, including input/font reads and writing the PDF. OS filesystem caches are not cleared; Python's bounded wait polling/scheduling overhead is included. This is not a physically cold disk benchmark.
- **Warm elapsed/allocation:** public Convert plus output MemoryStream; HTML/fonts are preloaded. GC.GetAllocatedBytesForCurrentThread measures same-thread cumulative allocation. Hashing/sample dictionaries/JSON are outside this interval. It is not total process allocation or live/peak memory.
- **Font loading:** separately records elapsed and current-thread allocation including explicit file reads and font parsing. Do not add it to an already end-to-end cold result.
- **Peak:** Linux wait4 observes each direct CLI child's process-lifetime high-water RSS, which may retain an inherited launcher floor; it is not established as an isolated post-exec engine peak. The warm host records whole-process lifetime PeakWorkingSet64. They are different scopes. Unavailable values are null with an explicit limitation, never zero. See the [reproducible launcher calibration](peak-calibration.md) and its actual control evidence.
- **Two concurrent conversions:** check isolated deterministic outputs only; not maximum throughput or a scalability benchmark.

The shared Actions machine is provisional, not approved stable reference hardware. No absolute millisecond, QPS or RSS promise is invented here. Actual result tables and candidate thresholds are produced only after the first real run; numerical budgets require review. A starting candidate alert policy is an output/hash mismatch as a correctness failure and a repeated same-environment >20% median regression as an investigation flag, **not an approved release SLA or automatic performance verdict**. Investigate repeated batches and reference-machine evidence before classifying timing noise as regression.

Human page-reference approval and fixed-machine budget approval remain explicit outstanding gates. No performance claim such as “ultra high performance achieved” follows from completing these measurements.
