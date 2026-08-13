# Case Study 01 — Multithreading Performance

## PowerPoint storyboard and presentation content

This document is a slide-by-slide guide for creating a `.pptx` presentation about **Case Study 01**. It is based on:

- the assignment requirement to improve calculation speed with manually created and managed threads;
- the original program in `Program-ori.cs`;
- the adjusted program on `master` at commit `2265263`;
- the layout style of `PTT AXIS_Claude_20260622.pdf`; and
- the measured Release-mode benchmark results collected on a machine with 22 logical processors.

The adjusted solution does **not** use `Parallel.For`, `Parallel.ForEach`, or another built-in parallel loop. It creates `Thread` objects, calls `Start()`, and waits for them with `Join()`.

---

# Presentation design guide

## Format

- Use a **16:9 widescreen** slide size.
- Use a clean white background for content slides.
- Use dark navy for headings and an orange accent for numbers, lines, and important results.
- Place a small uppercase label at the top-right of content slides, for example:  
  `CASE STUDY 01 — THREAD PERFORMANCE`
- Add a thin footer containing the course name, group name, and slide number.
- Keep each slide focused on one message.
- Use short bullets and diagrams instead of long paragraphs.

## Layout patterns inspired by the reference presentation

1. **Cover slide:** large title, short subtitle, date, course, and group.
2. **Agenda slide:** numbered agenda items with generous spacing.
3. **Section divider:** a large two-digit section number on the left and the section title on the right.
4. **Content slide:** one action-oriented headline followed by two or three columns.
5. **Comparison slide:** left-versus-right layout with a clear conclusion at the bottom.
6. **Results slide:** chart or table with one highlighted number and a short takeaway.
7. **Closing slide:** one conclusion statement followed by “Thank you”.

## Recommended typography

- Cover title: 34–42 pt
- Section-divider title: 30–36 pt
- Content-slide headline: 26–32 pt
- Body text: 17–21 pt
- Footer: 9–11 pt

## Writing style

Use conclusion-led titles rather than generic labels. For example:

- Weak: `Problem Identification`
- Strong: `The original program creates a thread but does not perform useful parallel work`

---

# Slide plan grouped by agenda

## Slide 1 — Cover

### Title

**Case Study 01: Increasing Calculation Speed with Threads**

### Subtitle

Manual static partitioning and dynamic chunk scheduling in C#

### Supporting information

- Operating Systems
- Group: `[Group number or name]`
- Team members: `[Names and student IDs]`
- Presentation date: `[Date]`

### Bottom label

`A MANUAL MULTITHREADING PERFORMANCE STUDY — NO PARALLEL.FOR`

### Visual direction

Use a simple illustration of one large dataset being distributed to several CPU workers.

---

## Slide 2 — Agenda

### Title

**Agenda**

### Content

01. Overview of the Original File  
02. Problem Identification  
03. Proposed Modifications  
04. Rationale for the Changes  
05. Team Members’ Proposed Solutions  
06. Approaches and Alternatives  
07. Results and Conclusions

### Speaker note

Explain that the presentation moves from understanding the original code to proving the improvement with measured results.

---

# Agenda 01 — Overview of the Original File

## Slide 3 — Section divider

### Content

**01**  
**Overview of the Original File**

---

## Slide 4 — Original program purpose

### Headline

**The original program performs 300 million calculations through one worker thread**

### Left column — Input

- Reads `11,000,001` values from `data.bin`.
- Each input is read as a 32-bit `float`.
- Each value is multiplied by `36`.
- The values are stored in a `decimal[]` array.

### Middle column — Calculation

- Processes the first `10,000,000` values.
- Repeats the calculation for `30` rounds.
- Calls `CalClass.Calculate1()` from an external DLL.
- Maximum calls: `10,000,000 × 30 = 300,000,000`.

### Right column — Output

- Uses a `Stopwatch` to measure calculation time.
- Waits for the worker with `Join()`.
- Prints elapsed milliseconds and the final result.
- Expected observed result: `4,686,980,924,312.00`.

### Bottom takeaway

**The workload is large and CPU-intensive, so it is a strong candidate for multicore execution.**

---

## Slide 5 — Original execution flow

### Headline

**The main thread loads the data, starts one worker, and immediately waits**

### Diagram

```text
Main thread
    │
    ├── Load 11,000,001 values
    │
    ├── Create Th1
    │
    ├── Start stopwatch
    │
    ├── Th1.Start()
    │       │
    │       └── 30 rounds × 10,000,000 values
    │
    ├── Th1.Join()
    │
    └── Stop timer and print result
```

### Key original variables

| Variable | Purpose | Access pattern |
|---|---|---|
| `data` | Stores all input values | Shared and modified |
| `index` | Selects the next value | Shared and modified |
| `result` | Stores the running total | Shared and modified |

### Speaker note

`Calculate1()` changes `data[index]`, increments `index`, and returns a value that is added to `result`. These side effects are important when considering multiple threads.

---

# Agenda 02 — Problem Identification

## Slide 6 — Section divider

### Content

**02**  
**Problem Identification**

---

## Slide 7 — Limited performance

### Headline

**Creating one worker thread does not provide useful CPU parallelism**

### What happens

- Only `Th1` performs the calculation.
- The main thread immediately waits at `Th1.Join()`.
- Other logical processors remain available but do not receive calculation work.
- Moving the calculation from the main thread to one worker does not make the work parallel.

### Evidence

- Original average calculation time: **34,640 ms**.
- The test machine exposes **22 logical processors**.
- The original implementation uses only one calculation worker.

### Bottom takeaway

**The main performance problem is unused processing capacity.**

---

## Slide 8 — Race conditions if a second thread is enabled

### Headline

**Uncommenting the second thread would make the original program unsafe**

### Three-column layout

#### Shared `index`

- Both threads can read the same index.
- An increment can be lost.
- One thread can reset the index while another thread is still processing a round.
- Values may be duplicated or skipped.

#### Shared `result`

- `result += value` is a read–modify–write operation.
- Two updates can overwrite each other.
- `decimal` updates are not atomic.
- The final result can change between runs.

#### Shared `data`

- `Calculate1()` modifies the selected array element.
- Two threads could modify the same value simultaneously.
- The order and number of mutations could become incorrect.

### Race-condition diagram

```text
Thread 1 reads index = 100 ──┐
                             ├── Both process data[100]
Thread 2 reads index = 100 ──┘

Thread 1 writes result = A ──┐
                             ├── One update may be lost
Thread 2 writes result = B ──┘
```

### Bottom takeaway

**Adding threads without separating ownership can make the answer incorrect.**

---

## Slide 9 — Secondary design issues

### Headline

**The original source also has reliability and maintainability limitations**

### Content

- The file stream and binary reader are not explicitly disposed.
- Constants such as `10,000,000` and `30` are embedded directly in loops.
- `using System;` is duplicated.
- `System.Text.Json` is imported but unused.
- The thread count cannot be selected at runtime.
- The program does not report how work was distributed.
- There is no check that exactly 10,000,000 values were processed.

### Priority note

These issues matter, but the first priority is to introduce **correct manual parallelism** without changing the calculation logic.

---

# Agenda 03 — Proposed Modifications

## Slide 10 — Section divider

### Content

**03**  
**Proposed Modifications**

---

## Slide 11 — New multithreading design

### Headline

**The adjusted program gives each value to exactly one manually managed worker**

### Proposed changes

- Let the user choose `1–22` worker threads.
- Create the workers manually with `new Thread(...)`.
- Start each worker with `Start()`.
- Wait for every worker with `Join()`.
- Replace the shared running result with one local result per worker.
- Ensure that workers never own overlapping array regions.
- Combine local results only after all workers have completed.
- Display the values and chunks processed by every worker.
- Verify that the total processed value count equals `10,000,000`.

### Compliance statement

**The solution does not use `Parallel.For`, `Parallel.ForEach`, or another built-in parallel loop.**

---

## Slide 12 — Proposal A: static range partitioning

### Headline

**Static partitioning divides the dataset into fixed, non-overlapping ranges**

### Formula

```text
start = TotalValues × WorkerID ÷ ThreadCount
end   = TotalValues × (WorkerID + 1) ÷ ThreadCount
```

### Example with four threads

| Worker | Assigned indices |
|---:|---:|
| 1 | 0–2,499,999 |
| 2 | 2,500,000–4,999,999 |
| 3 | 5,000,000–7,499,999 |
| 4 | 7,500,000–9,999,999 |

### Advantages

- No shared calculation index.
- No overlap between workers.
- Very low synchronization overhead.
- Simple and predictable work assignment.

### Limitation

If one range takes longer than another, fast workers may finish early and become idle.

---

## Slide 13 — Proposal B: dynamic chunk scheduling

### Headline

**Dynamic scheduling lets free workers claim the next 50,000-value chunk**

### Diagram

```text
Shared nextChunkStart
          │
          └── Interlocked.Add(..., 50,000)
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
     Worker 1       Worker 2       Worker 3
      chunk 0        chunk 1        chunk 2
        │              │              │
        └── finish and claim another chunk ──┘
```

### How it works

- The calculation range is divided into `50,000`-value chunks.
- `Interlocked.Add()` atomically assigns a unique chunk start.
- A worker processes all 30 rounds for its chunk.
- After finishing, the worker requests another chunk.
- Faster workers naturally process more chunks.

### Safety property

**Each chunk has one owner, so two workers do not modify the same array element.**

---

## Slide 14 — Safe result aggregation and validation

### Headline

**Private accumulation removes result contention from the calculation loop**

### Per-worker state

- `localResult`: sum calculated by one worker.
- `processedValues[id]`: number of values processed by that worker.
- `processedChunks[id]`: number of work chunks completed.

### Main-thread responsibility

1. Start all workers.
2. Wait for all workers with `Join()`.
3. Add every entry in `localResults`.
4. Add the processed-value and chunk counts.
5. Reject the run if the total value count is not `10,000,000`.

### Supporting improvements

- Use named constants for data length, calculation count, rounds, maximum threads, and chunk size.
- Use `using` declarations to close the input stream safely.
- Validate numeric user input.
- Name workers as `Worker-1`, `Worker-2`, and so on.

---

# Agenda 04 — Rationale for the Changes

## Slide 15 — Section divider

### Content

**04**  
**Rationale for the Changes**

---

## Slide 16 — Design rationale

### Headline

**The design improves speed by reducing shared mutable state rather than locking the whole calculation**

| Change | Rationale | Expected benefit |
|---|---|---|
| Multiple manual threads | Use more logical processors | Lower execution time |
| Non-overlapping ownership | Prevent two workers from changing one value | Correct, repeatable output |
| Local indices | Remove the shared-index race | No skipped or duplicated values |
| Local results | Avoid `decimal` update races | Correct totals with little contention |
| Static ranges | Minimize synchronization | Strong performance for uniform work |
| Dynamic chunks | Reassign work when a worker becomes free | Better load balancing |
| `Interlocked.Add()` per chunk | Claim work atomically | Unique chunks with low overhead |
| `Join()` before reduction | Ensure all local results are complete | Safe final aggregation |
| Worker statistics | Make distribution visible | Easier correctness and performance analysis |

### Bottom takeaway

**Clear data ownership enables real parallel work without placing a lock around every DLL call.**

---

# Agenda 05 — Team Members’ Proposed Solutions

## Slide 17 — Section divider

### Content

**05**  
**Team Members’ Proposed Solutions**

---

## Slide 18 — Team contribution map

### Headline

**The final implementation combines scheduling, safety, validation, and measurement proposals**

Replace the placeholders below with the actual names and contributions before creating the final `.pptx`.

| Team member | Proposed idea | Contribution to the final solution |
|---|---|---|
| `[Member 1]` | Static range partitioning | Formula for equal, non-overlapping index ranges |
| `[Member 2]` | Dynamic chunk scheduling | Atomic chunk claiming with `Interlocked.Add()` |
| `[Member 3]` | Thread-safe result handling | Per-worker local results and final reduction |
| `[Member 4]` | Testing and validation | 1–22 thread benchmark, averages, correctness checks |
| `[Member 5, if applicable]` | Presentation and code review | Diagrams, comparison, conclusions, and source cleanup |

### Optional speaker note

If the group has fewer members, combine related rows. If it has more members, split testing, documentation, and presentation design into separate roles. Only claim work that each member actually performed.

---

# Agenda 06 — Approaches and Alternatives

## Slide 19 — Section divider

### Content

**06**  
**Approaches and Alternatives**

---

## Slide 20 — Static versus dynamic scheduling

### Headline

**Static ranges minimize overhead, while dynamic chunks improve load balancing**

| Criterion | Static ranges | Dynamic chunks |
|---|---|---|
| Assignment | One fixed range per worker | Workers repeatedly claim chunks |
| Synchronization | Almost none during calculation | One atomic operation per chunk |
| Load balancing | Good when ranges take equal time | Good when work time is uneven |
| Worker utilization | Some workers may finish early | Free workers continue taking work |
| Complexity | Lower | Moderate |
| Current implementation | Yes | Yes, 50,000 values per chunk |

### Recommendation

- Use **static ranges** when the cost per value is uniform and synchronization overhead should be minimal.
- Use **dynamic chunks** when the cost is uneven or the CPU contains cores with different performance characteristics.
- Benchmark both modes on the target computer before choosing the final configuration.

---

## Slide 21 — Alternatives considered

### Headline

**Several simpler alternatives were rejected because they reduce safety, performance, or assignment compliance**

| Alternative | Why it was not selected |
|---|---|
| Uncomment `Th2` without redesign | Creates races on `index`, `result`, and `data` |
| Lock every `Calculate1()` call | Correct but serializes the expensive work and reduces speedup |
| One thread per array value | Millions of threads would create extreme memory and scheduling overhead |
| Atomic claim for every individual value | More synchronization than chunk-level claiming |
| `Parallel.For` / `Parallel.ForEach` | Explicitly prohibited by the assignment |
| `Task.Run` or ThreadPool-only design | Hides thread creation and management, weakening assignment compliance |
| CPU affinity and manual core pinning | Platform-specific and unnecessary for the core solution |

### Bottom takeaway

**Manual worker threads with separated data ownership provide the best balance of compliance, correctness, and speed.**

---

# Agenda 07 — Results and Conclusions

## Slide 22 — Section divider

### Content

**07**  
**Results and Conclusions**

---

## Slide 23 — Benchmark method

### Headline

**Three Release-mode trials per thread count were averaged to reduce timing noise**

### Test conditions

- Environment: `.NET 9`, Release build
- Available logical processors: `22`
- Thread counts tested: `1–22`
- Trials per thread count: `3`
- Timed section: calculation only; data loading was excluded
- Each run started with newly loaded data because `Calculate1()` modifies the array
- Baseline: original single-worker program
- Original trials: `34,671`, `34,678`, and `34,571` ms
- Original average: **34,640 ms**
- Correct result observed in every benchmark run: `4,686,980,924,312.00`

### Formula

```text
Average time = (Trial 1 + Trial 2 + Trial 3) ÷ 3
Speedup      = Original average ÷ Adjusted average
```

### Accuracy note

The collected 1–22 results below measure the adjusted **static-range** implementation. Dynamic scheduling should be benchmarked separately under the same conditions before making a measured static-versus-dynamic performance claim.

---

## Slide 24 — Results for 1–11 threads

### Headline

**Performance scales strongly as the first eleven workers are added**

| Threads | Average time (ms) | Speedup vs. original |
|---:|---:|---:|
| 1 | 33,454 | 1.04× |
| 2 | 16,872 | 2.05× |
| 3 | 11,304 | 3.06× |
| 4 | 8,626 | 4.02× |
| 5 | 7,009 | 4.94× |
| 6 | 5,703 | 6.07× |
| 7 | 5,738 | 6.04× |
| 8 | 5,399 | 6.42× |
| 9 | 5,095 | 6.80× |
| 10 | 4,738 | 7.31× |
| 11 | 4,322 | 8.01× |

### Visual direction

Use a line chart with thread count on the horizontal axis and average time in milliseconds on the vertical axis. Highlight the rapid reduction from one to six threads.

---

## Slide 25 — Results for 12–22 threads

### Headline

**Twenty-one threads produced the best average time on the test machine**

| Threads | Average time (ms) | Speedup vs. original |
|---:|---:|---:|
| 12 | 4,032 | 8.59× |
| 13 | 3,874 | 8.94× |
| 14 | 3,653 | 9.48× |
| 15 | 3,440 | 10.07× |
| 16 | 3,235 | 10.71× |
| 17 | 3,177 | 10.90× |
| 18 | 3,149 | 11.00× |
| 19 | 3,086 | 11.22× |
| 20 | 2,896 | 11.96× |
| **21** | **2,816** | **12.30×** |
| 22 | 2,839 | 12.20× |

### Highlight boxes

- **Best average:** 21 threads — 2,816 ms
- **Speedup:** 12.30× faster than the original average
- **Time reduction:** approximately 91.9%
- **Fastest individual run:** 22 threads — 2,699 ms

### Speaker note

Twenty-two threads did not give the best average. Extra workers can introduce scheduling, memory-bandwidth, cache, and resource-contention overhead. The best thread count should therefore be measured rather than assumed.

---

## Slide 26 — Results summary

### Headline

**Manual range partitioning reduced average calculation time from 34.64 seconds to 2.82 seconds**

### Before-and-after comparison

| Metric | Original | Best adjusted result |
|---|---:|---:|
| Calculation workers | 1 | 21 |
| Average time | 34,640 ms | 2,816 ms |
| Relative speed | 1.00× | 12.30× |
| Time reduction | — | 91.9% |
| Result | 4,686,980,924,312.00 | 4,686,980,924,312.00 |

### Visual direction

Use two large vertical bars:

- Original: `34.64 s`
- Adjusted: `2.82 s`

Place a large orange label between them: **12.30× faster**.

---

## Slide 27 — Conclusions

### Headline

**Correct workload ownership made the calculation faster without changing its result**

### Conclusions

1. The original program created a thread but used only one calculation worker.
2. Simply enabling another original worker would introduce race conditions.
3. Non-overlapping ranges and local results allow safe parallel execution.
4. Dynamic chunks provide an additional strategy for balancing uneven work.
5. Manual `Thread`, `Start()`, and `Join()` satisfy the assignment requirements.
6. The best measured static-range configuration used 21 threads.
7. Average calculation time fell from 34,640 ms to 2,816 ms.
8. The best configuration was 12.30× faster while preserving the final result.

### Final statement

**More threads do not automatically guarantee better performance; correct ownership, low synchronization overhead, and measurement are essential.**

---

## Slide 28 — Thank you

### Content

**Thank you**

Questions and discussion

### Optional footer

Repository commit presented: `2265263`  
Scheduling modes: static ranges and dynamic 50,000-value chunks

---

# Appendix A — Full benchmark data

Use this table as a backup slide or as the source data for PowerPoint charts.

| Threads | Trial 1 (ms) | Trial 2 (ms) | Trial 3 (ms) | Average (ms) | Speedup |
|---:|---:|---:|---:|---:|---:|
| 1 | 33,435 | 33,529 | 33,399 | 33,454 | 1.04× |
| 2 | 16,910 | 16,981 | 16,726 | 16,872 | 2.05× |
| 3 | 11,217 | 11,351 | 11,345 | 11,304 | 3.06× |
| 4 | 8,644 | 8,526 | 8,707 | 8,626 | 4.02× |
| 5 | 7,142 | 7,040 | 6,844 | 7,009 | 4.94× |
| 6 | 5,726 | 5,710 | 5,674 | 5,703 | 6.07× |
| 7 | 5,521 | 5,863 | 5,829 | 5,738 | 6.04× |
| 8 | 5,209 | 5,249 | 5,739 | 5,399 | 6.42× |
| 9 | 5,114 | 5,047 | 5,123 | 5,095 | 6.80× |
| 10 | 4,740 | 4,712 | 4,762 | 4,738 | 7.31× |
| 11 | 4,347 | 4,274 | 4,345 | 4,322 | 8.01× |
| 12 | 3,955 | 4,069 | 4,072 | 4,032 | 8.59× |
| 13 | 3,797 | 3,924 | 3,901 | 3,874 | 8.94× |
| 14 | 3,646 | 3,640 | 3,674 | 3,653 | 9.48× |
| 15 | 3,382 | 3,553 | 3,385 | 3,440 | 10.07× |
| 16 | 3,315 | 3,175 | 3,214 | 3,235 | 10.71× |
| 17 | 3,236 | 3,208 | 3,088 | 3,177 | 10.90× |
| 18 | 3,046 | 3,301 | 3,101 | 3,149 | 11.00× |
| 19 | 3,183 | 3,132 | 2,944 | 3,086 | 11.22× |
| 20 | 2,728 | 3,002 | 2,959 | 2,896 | 11.96× |
| 21 | 2,958 | 2,775 | 2,715 | 2,816 | 12.30× |
| 22 | 2,920 | 2,899 | 2,699 | 2,839 | 12.20× |

Original baseline trials: `34,671`, `34,678`, and `34,571` ms.  
Original baseline average: `34,640` ms.

---

# Appendix B — Suggested presentation visuals

## Visual 1 — Original flow

Show the main thread loading data, starting one worker, and waiting.

## Visual 2 — Race condition

Show two workers pointing to the same array index and writing to the same result.

## Visual 3 — Static ranges

Draw one horizontal data bar split into equal colored sections, one per worker.

## Visual 4 — Dynamic chunks

Draw a central queue of chunks with arrows to three workers; show a faster worker receiving more chunks.

## Visual 5 — Performance curve

Create a line chart using the average times from 1–22 threads.

## Visual 6 — Before and after

Use two bars comparing `34.64 s` with `2.82 s`, with `12.30× faster` as the central message.

---

# Final checklist before exporting the `.pptx`

- Replace all team-member placeholders with real names and verified contributions.
- Add the course name, group number, date, and student IDs.
- Keep the agenda numbering consistent across section-divider slides.
- Use no more than five or six short bullets on a visual slide.
- Put detailed explanations in speaker notes, not on the slide.
- Use the full benchmark table only as an appendix or split it across two slides.
- State that the measured 1–22 results are for static range partitioning.
- Do not claim measured dynamic-mode speedup until that mode is benchmarked separately.
- Confirm that all performance runs use a Release build and freshly loaded data.
- Confirm that the displayed final result is `4,686,980,924,312.00`.
- Include the assignment-compliance statement: manually created threads, no `Parallel.For`.
- Proofread slide titles and ensure every title communicates the slide’s main conclusion.
