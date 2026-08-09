# Case Study 01: Multithreading

## 1. Main idea

`CaseStudy01` is a C#/.NET 9 program about **multithreading**. It loads about 11 million numbers from a binary file, performs a large calculation in a worker thread, measures the execution time, and prints the final result.

The program is prepared for an experiment with one or two threads. However, the current version enables only one worker thread. If the second thread is enabled, the program has serious race conditions.

## 2. Important files

| File or folder | Purpose |
|---|---|
| `Program.cs` | Contains the main source code. |
| `CaseStudy01.csproj` | Contains the .NET project settings and DLL reference. |
| `CaseStudy01.sln` | Opens the project as a Visual Studio solution. |
| `data.bin` | Binary input containing 11,000,001 `float` values. |
| `DLL/CalculatingFunctions.dll` | External library containing `CalClass.Calculate1()`. |
| `DLL/CalculatingFunctions.dll.old` | An older backup DLL; the project does not use it. |
| `bin/` | Contains the compiled program and copied dependencies. |
| `obj/` | Contains temporary files used during compilation. |
| `.vs/` | Contains local Visual Studio settings and cache files. |

The binary file is exactly 44,000,004 bytes:

```text
11,000,001 values × 4 bytes per float = 44,000,004 bytes
```

The two DLL files have the same size and version information, but their binary contents are different. Only `CalculatingFunctions.dll` is referenced by the project.

## 3. Project configuration

The project file contains:

```xml
<OutputType>Exe</OutputType>
<TargetFramework>net9.0</TargetFramework>
```

This means:

- The output is an executable program.
- The program requires .NET 9.
- Nullable reference checking is enabled.
- Common namespaces are automatically imported.

The project also references an external library:

```xml
<Reference Include="CalculatingFunctions">
  <HintPath>.\DLL\CalculatingFunctions.dll</HintPath>
</Reference>
```

Therefore, `Program.cs` can import the library and create a calculator object:

```csharp
using CalculatingFunctions;

CalClass CF = new CalClass();
```

The source code for `CalClass` is not included. The large comment in `Program.cs` describes the expected implementation of `Calculate1()`.

## 4. Shared variables

The program declares three static variables:

```csharp
static decimal[] data = new decimal[11000001];
static decimal result = 0;
static long index = 0;
```

### `data`

This array holds 11,000,001 decimal numbers.

A C# `decimal` normally uses 16 bytes, so the elements require approximately:

```text
11,000,001 × 16 = 176,000,016 bytes
```

That is about 168 MiB, plus a small amount of array information.

### `result`

This stores the total of all returned calculation results.

### `index`

This tells `Calculate1()` which array element it should process.

All three variables are `static`, so every thread uses the same variables. This becomes dangerous when two threads are enabled.

## 5. Complete execution flow

```text
Main thread starts
       │
       ▼
LoadData()
Read 11,000,001 floats
Convert them to decimal
       │
       ▼
Create worker thread Th1
       │
       ▼
Start stopwatch
       │
       ▼
Start Th1
       │
       ├── Repeat 30 times
       │      ├── Set index = 0
       │      └── Call Calculate1() 10,000,000 times
       │
       ▼
Main thread waits with Join()
       │
       ▼
Stop stopwatch
Print time and result
```

## 6. Loading the data

The `LoadData()` method opens the input file:

```csharp
FileStream fs = new FileStream("data.bin", FileMode.Open);
BinaryReader br = new BinaryReader(fs);
```

The path is relative to the program's **current working directory**. Therefore, the program normally needs to run with `CaseStudy01/` as its working directory. Otherwise, it may report that `data.bin` cannot be found.

The loop reads one 32-bit floating-point value at a time:

```csharp
Single f = br.ReadSingle();
data[i] = (decimal)(f * 36);
```

For every value:

1. `ReadSingle()` reads four bytes.
2. The bytes become a `float`.
3. The value is multiplied by 36.
4. It is converted to `decimal`.
5. It is stored in the array.

For example, the first raw value is approximately `231607.02`. After multiplication, the stored value is approximately:

```text
231607.02 × 36 ≈ 8,337,852.72
```

One problem is that `BinaryReader` and `FileStream` are not explicitly closed. A safer implementation would use `using`:

```csharp
using FileStream fs = new FileStream("data.bin", FileMode.Open);
using BinaryReader br = new BinaryReader(fs);
```

If the file is shorter than expected, `ReadSingle()` will throw an `EndOfStreamException`.

## 7. Worker-thread calculation

The `ThreadWork()` method first creates one calculator object:

```csharp
CalClass CF = new CalClass();
```

It then performs 30 rounds:

```csharp
while (i < 30)
```

At the beginning of each round, the shared index is reset:

```csharp
index = 0;
```

The inner loop processes indices `0` through `9,999,999`:

```csharp
while (index < 10000000)
{
    result += CF.Calculate1(ref data, ref index);
}
```

With one thread, the expected number of calls is:

```text
30 rounds × 10,000,000 calls = 300,000,000 calls
```

The array contains 11,000,001 elements, but only 10,000,000 elements are processed. The final 1,000,001 elements are loaded but never calculated.

## 8. The `Calculate1()` algorithm

The actual method is inside `CalculatingFunctions.dll`. The comment in `Program.cs` describes its expected algorithm.

### Step 1: Select the element

```csharp
i = idx;
```

Normally, `i` is the current shared index.

There is also a safety check:

```csharp
if (i >= value.Length)
{
    i = value.Length - 1;
}
```

If the index is too large, the method uses the final array element.

### Step 2: Test divisibility

The method uses:

```csharp
(int)value[i]
```

This removes the fractional part. For example:

```text
12.9 becomes 12
7.8 becomes 7
```

It then checks divisibility in this order:

| Condition | Calculation |
|---|---:|
| Divisible by 2 | `value × 0.2` |
| Otherwise, divisible by 3 | `value × 0.3` |
| Otherwise, divisible by 5 | `value × 0.5` |
| Otherwise, divisible by 7 | `value × 0.7` |
| None of these | `value × 0.1` |

Because the code uses `else if`, only the first matching condition is selected.

For example, `30` is divisible by 2, 3, and 5. The program selects the first condition, so it uses 20%, not 30% or 50%.

### Step 3: Decide whether the result is positive or negative

The program converts `sum` to `long`, which removes its fractional part:

```csharp
if ((long)sum % 2 == 0)
```

If that integer is even:

```csharp
result = Math.Round(sum * 0.5m);
```

If it is odd:

```csharp
result = Math.Round(-sum * 0.3m);
```

`Math.Round()` rounds to a whole number. By default, an exact midpoint uses **round to even**. For example, `2.5` becomes `2`, while `3.5` becomes `4`.

### Step 4: Change the original data

```csharp
value[i] *= 0.1m;
```

This is important: the method does not only read the value. It changes it to 10% of its old value.

Therefore, every round uses a smaller value than the previous round:

```text
Original value
Round 1: value × 0.1
Round 2: value × 0.01
Round 3: value × 0.001
...
```

After 30 rounds, the value has been multiplied by approximately `10⁻³⁰`.

### Step 5: Advance the index

```csharp
idx++;
return result;
```

Because `idx` is passed with `ref`, this changes the `index` variable in `Program`.

### Example

Suppose the current value is `30`:

1. `30` is divisible by 2.
2. `sum = 30 × 0.2 = 6`.
3. `6` is even.
4. The returned result is `Round(6 × 0.5) = 3`.
5. The stored array value becomes `30 × 0.1 = 3`.
6. The index increases by one.

During the next round, the same element starts with `3`, not `30`.

## 9. Creating and controlling the thread

The program creates one explicit worker thread:

```csharp
Thread Th1 = new Thread(ThreadWork);
```

`Th1.Start()` asks the runtime and operating system to schedule it:

```csharp
Th1.Start();
```

The main thread then calls:

```csharp
Th1.Join();
```

`Join()` means:

> Wait here until `Th1` has completely finished.

Without `Join()`, the main thread could stop the timer and print the result before the worker had finished its calculation.

The .NET worker is normally connected to an operating-system thread. The OS scheduler decides when it runs and which CPU core runs it.

## 10. Timing

The stopwatch starts after data loading:

```csharp
_st.Start();
Th1.Start();
Th1.Join();
_st.Stop();
```

Therefore, the reported time includes:

- Starting the worker thread
- All 300 million calculations
- Waiting for the worker
- Thread-completion overhead

It does not include:

- Reading `data.bin`
- Converting floats to decimals
- Creating the `Thread` object

The final output uses:

```csharp
result.ToString("F2")
```

This prints the result with exactly two decimal places.

## 11. Is it really parallel now?

Not in a useful way.

The program creates a worker thread, but the main thread immediately waits at `Join()`. Only `Th1` performs the calculation. Moving the calculation from the main thread to one worker thread does not create CPU parallelism.

The current program mainly demonstrates:

- How to create a thread
- How to start a thread
- How the main thread waits
- How to measure threaded work

The .NET runtime may have other internal threads, such as garbage-collection threads, but the program creates only one calculation worker.

## 12. What happens if `Th2` is enabled?

The commented lines create a second worker:

```csharp
Thread Th2 = new Thread(ThreadWork);
Th2.Start();
Th2.Join();
```

This may look like a simple way to use two CPU cores. However, it is not thread-safe because both threads share `data`, `result`, and `index`.

### Race on `index`

Both threads may read the same index:

```text
Thread 1 reads index = 100
Thread 2 reads index = 100
Thread 1 processes data[100]
Thread 2 also processes data[100]
```

Also, `idx++` is a read-modify-write operation. It is not atomic, so one increment can be lost.

A worse problem happens when one thread finishes a round and runs:

```csharp
index = 0;
```

It may reset the index while the other thread is still working on the previous round.

### Race on `result`

This operation is not atomic:

```csharp
result += calculatedValue;
```

Internally, it works like this:

1. Read the old result.
2. Add a value.
3. Write the new result.

Two threads can read the same old result and overwrite each other's updates. A `decimal` is also a large 128-bit value, so ordinary reads and writes are not guaranteed to be safely atomic.

### Race on `data`

Both threads can change the same element:

```csharp
value[i] *= 0.1m;
```

An element might be multiplied twice, once, or updated in an unexpected order.

### Result of these races

With two threads:

- The answer may change between runs.
- Some elements may be processed more than once.
- Some elements may be skipped.
- Updates to `result` may be lost.
- The program may perform more or less work than expected.

This is called a **race condition**: the result depends on the exact timing of the threads.

## 13. Why a simple lock is not the best solution

A lock could protect the shared calculation:

```csharp
lock (someObject)
{
    result += CF.Calculate1(ref data, ref index);
}
```

This would improve correctness, but only one thread could calculate at a time. The second thread would spend much of its time waiting. Therefore, the program would probably not become faster.

A better design is:

- Give each thread a different range of array indices.
- Do not use one shared index.
- Give each thread a private/local result.
- Ensure that two threads never change the same array element.
- Combine the local results after both threads finish.

For example:

```text
Thread 1: indices 0–4,999,999
Thread 2: indices 5,000,000–9,999,999
```

This approach reduces synchronization and allows real parallel work.

## 14. Code-quality observations

- `using System;` appears twice.
- `System.Text.Json` is imported but not used.
- `Main(string[] args)` does not use `args`.
- Passing `data` with `ref` is probably unnecessary. Arrays are reference types, so a method can modify their elements without `ref`.
- The data-file path depends on the working directory.
- The file streams should be disposed with `using`.
- Generated folders such as `.vs/`, `bin/`, and `obj/` can normally be regenerated.
- `CalculatingFunctions.dll.old` is only a backup and has no effect on the build.

## 15. Main conclusion

This case study is a **threading benchmark experiment**, but its current safe configuration uses only one calculation thread. It performs 300 million DLL calls and shows how `Start()`, `Join()`, and `Stopwatch` work.

The most important lesson is that adding a second thread is not automatically correct or faster. Threads must have clearly separated work. Shared writable variables such as `index`, `result`, and `data` need careful design to prevent race conditions.
