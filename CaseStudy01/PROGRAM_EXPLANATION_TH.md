# คำอธิบายโปรแกรม Case Study 01: Manual Multithreading

## 1. วัตถุประสงค์ของงาน

โจทย์กำหนดให้เพิ่มความเร็วของการคำนวณด้วย **threads** โดยมีเงื่อนไขสำคัญว่า ห้ามใช้คำสั่งจัดการเธรดสำเร็จรูป เช่น `Parallel.For` หรือ `Parallel.ForEach` ผู้พัฒนาต้องสร้าง เริ่มต้น รอ และจัดสรรงานให้เธรดด้วยตนเอง

โปรแกรมใน `Program.cs` ทำตามเงื่อนไขดังกล่าวโดย:

- สร้าง worker ด้วย `new Thread(...)`
- เริ่มแต่ละ worker ด้วย `Thread.Start()`
- รอให้ worker ทุกตัวทำงานเสร็จด้วย `Thread.Join()`
- ใช้ `Interlocked.Add()` แจกงานโดยไม่ให้ chunk ซ้ำกัน
- ไม่ใช้ `Parallel.For`, `Parallel.ForEach`, `Task.Run` หรือ thread pool
- รักษาผลลัพธ์สุดท้ายให้เท่ากับ `4686980924312.00`

## 2. ภาพรวมการทำงาน

ลำดับการทำงานของโปรแกรมคือ:

```text
อ่านข้อมูลจาก data.bin
        ↓
ตรวจหาจำนวน logical processors
        ↓
รับจำนวน worker threads จากผู้ใช้
        ↓
สร้าง Thread[] และเริ่มทุก thread ด้วยตนเอง
        ↓
แต่ละ thread ขอ chunk ถัดไปด้วย Interlocked.Add()
        ↓
คำนวณแต่ละค่าได้สูงสุด 30 รอบ
        ↓
เก็บผลลัพธ์แยกตาม thread
        ↓
Main thread เรียก Join() เพื่อรอทุก worker
        ↓
รวมผลลัพธ์และตรวจสอบความถูกต้อง
```

## 3. Namespace ที่ใช้

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CalculatingFunctions;
```

- `System.Diagnostics` ใช้ `Stopwatch` สำหรับจับเวลาการคำนวณ
- `System.Runtime.CompilerServices` ใช้ส่งคำแนะนำการ optimize ให้ JIT compiler
- `CalculatingFunctions` เป็น DLL ของโจทย์ ซึ่งมีคลาส `CalClass` และเมธอด `Calculate1()`

## 4. ค่าคงที่ของโปรแกรม

```csharp
private const int DataLength = 11_000_001;
private const int CalculationCount = 10_000_000;
private const int CalculationRounds = 30;
private const decimal ExpectedResult = 4_686_980_924_312m;
```

ความหมายของแต่ละค่าคือ:

| ชื่อ | ค่า | ความหมาย |
|---|---:|---|
| `DataLength` | 11,000,001 | จำนวนสมาชิกทั้งหมดในอาร์เรย์ |
| `CalculationCount` | 10,000,000 | จำนวนสมาชิกที่นำมาคำนวณจริง |
| `CalculationRounds` | 30 | จำนวนรอบสูงสุดสำหรับแต่ละค่า |
| `ExpectedResult` | 4,686,980,924,312 | ผลลัพธ์ที่ถูกต้องของข้อมูลชุดนี้ |

เครื่องหมาย `_` ในตัวเลขช่วยให้อ่านง่าย แต่ไม่มีผลต่อค่าจริง เช่น `10_000_000` เท่ากับ `10000000` ส่วนตัวอักษร `m` ระบุว่าตัวเลขนั้นเป็นชนิด `decimal`

แม้อาร์เรย์จะมี 11,000,001 ตำแหน่ง แต่โปรแกรมคำนวณเพียง index `0` ถึง `9,999,999` รวมทั้งหมด 10,000,000 ค่า ตามพฤติกรรมของโปรแกรมต้นฉบับ

## 5. การแบ่งงานเป็น Dynamic Chunks

```csharp
private const int ChunkSize = 16_384;
```

โปรแกรมแบ่งข้อมูล 10,000,000 ค่าออกเป็น chunk ขนาดไม่เกิน 16,384 ค่า ทำให้มีงานประมาณ 611 chunks

โปรแกรมไม่ได้กำหนดช่วงขนาดใหญ่แบบตายตัวให้แต่ละ thread แต่ให้ thread ขอ chunk ใหม่เมื่อทำ chunk เดิมเสร็จ วิธีนี้เรียกว่า **dynamic chunk scheduling**

ข้อดีคือ:

- thread ที่ทำงานเร็วสามารถรับงานเพิ่มได้ทันที
- thread ที่ทำงานช้าจะได้รับจำนวน chunk น้อยกว่า
- ลดเวลาที่ core บางตัวว่าง ขณะที่ core อื่นยังมีงานจำนวนมาก
- เหมาะกับ CPU ที่มีทั้ง P-core และ E-core

โปรแกรมไม่ได้บังคับว่า thread ใดต้องทำงานบน P-core หรือ E-core เพราะระบบปฏิบัติการเป็นผู้เลือก core ให้ thread แต่ dynamic scheduling ทำให้ worker ที่ทำงานบน core ที่เร็วกว่าเสร็จเร็วและกลับมารับ chunk ใหม่ได้มากกว่าโดยอัตโนมัติ

ตัวอย่าง:

```text
P-core ทำ chunk เสร็จเร็ว → ขอ chunk ใหม่เร็ว → ทำงานรวมมากกว่า
E-core ทำ chunk ช้ากว่า      → ขอ chunk ใหม่น้อยกว่า → ทำงานรวมน้อยกว่า
```

## 6. การข้ามรอบที่ไม่ส่งผลต่อคำตอบ

```csharp
private const decimal NearZeroThreshold = 5m;
```

เมธอด `Calculate1()` แก้ค่าต้นฉบับหลังคำนวณแต่ละรอบด้วยการคูณ `0.1`

ตัวอย่าง:

```text
ค่าเริ่มต้น 300
หลังรอบที่ 1 = 30
หลังรอบที่ 2 = 3
หลังรอบที่ 3 = 0.3
หลังรอบที่ 4 = 0.03
```

ภายใต้กฎของ `Calculate1()` เมื่อค่าสัมบูรณ์ต่ำกว่า `5` ผลที่คืนหลังการคำนวณและปัดเศษจะเป็นศูนย์ เมื่อค่าถูกคูณด้วย `0.1` รอบต่อไปก็จะยิ่งเล็กลงและยังคงให้ผลเป็นศูนย์

โปรแกรมจึงหยุดรอบที่เหลือด้วย:

```csharp
if (Math.Abs(Data[index]) < NearZeroThreshold)
{
    break;
}
```

การหยุดนี้ลดจำนวนครั้งที่เรียก DLL โดยไม่เปลี่ยนผลรวมสุดท้าย

## 7. ตัวแปรส่วนกลาง

```csharp
private static decimal[] Data = new decimal[DataLength];
private static decimal[] localResults = Array.Empty<decimal>();
private static int[] processedValues = Array.Empty<int>();
private static int[] processedChunks = Array.Empty<int>();
private static int nextChunkStart;
```

### 7.1 `Data`

`Data` เก็บข้อมูลที่อ่านจาก `data.bin` และต้องเป็น `decimal[]` เนื่องจาก DLL กำหนดรูปแบบเมธอดเป็น:

```csharp
Calculate1(ref decimal[] value, ref long idx)
```

ถ้าเปลี่ยนเป็น `float[]` หรือ `double[]` จะส่งเข้าเมธอดนี้ไม่ได้ และหากเขียนอัลกอริทึมเลียนแบบด้วย floating-point ผลลัพธ์อาจไม่ตรงกับโปรแกรมต้นฉบับ

### 7.2 `localResults`

เก็บผลลัพธ์แยกตาม worker เช่น เมื่อมี 4 workers:

```text
localResults[0] = ผลจาก Worker 1
localResults[1] = ผลจาก Worker 2
localResults[2] = ผลจาก Worker 3
localResults[3] = ผลจาก Worker 4
```

แต่ละ worker เขียนเฉพาะตำแหน่งของตัวเอง จึงไม่เกิดการแย่งกันแก้ผลรวมและไม่ต้องใช้ lock ในทุกครั้งที่บวกผลลัพธ์

### 7.3 `processedValues` และ `processedChunks`

- `processedValues` เก็บจำนวนค่าที่แต่ละ worker ประมวลผล
- `processedChunks` เก็บจำนวน chunk ที่แต่ละ worker ได้รับ

ข้อมูลนี้ใช้ตรวจสอบการแจกงานและแสดงให้เห็นว่า worker ที่เร็วสามารถรับ chunk ได้มากกว่า

### 7.4 `nextChunkStart`

เก็บ index เริ่มต้นของ chunk ถัดไป เป็นตัวแปรที่ทุก thread ใช้ร่วมกัน จึงต้องอัปเดตด้วย atomic operation เพื่อป้องกันการได้งานซ้ำกัน

## 8. การคำนวณหนึ่งค่า

```csharp
[MethodImpl(
    MethodImplOptions.AggressiveInlining |
    MethodImplOptions.AggressiveOptimization)]
private static decimal CalculateValue(CalClass calculator, int index)
```

`AggressiveInlining` และ `AggressiveOptimization` เป็นคำแนะนำให้ JIT compiler พยายาม optimize เมธอดที่ถูกเรียกบ่อย แต่ไม่รับประกันว่า compiler จะ inline เมธอดเสมอ

ตัวแปรภายในมีดังนี้:

```csharp
decimal result = 0m;
long calculatorIndex = index;
```

- `result` เก็บผลรวมของข้อมูลหนึ่งตำแหน่งจากหลายรอบ
- `calculatorIndex` ต้องเป็น `long` เพราะ DLL รับ `ref long`

แม้ index สูงสุดเพียง 10 ล้านและใช้ `int` ได้ แต่ตัวแปรที่ส่งให้ DLL จำเป็นต้องเป็น `long` ตาม signature ของเมธอด

### 8.1 คำนวณแต่ละค่าได้สูงสุด 30 รอบ

```csharp
for (int round = 0; round < CalculationRounds; round++)
```

ลำดับใหม่เป็นดังนี้:

```text
Data[0] → คำนวณได้สูงสุด 30 รอบ
Data[1] → คำนวณได้สูงสุด 30 รอบ
Data[2] → คำนวณได้สูงสุด 30 รอบ
...
```

ไม่ใช่การทำข้อมูลทั้ง 10 ล้านค่าหนึ่งรอบ แล้วจึงย้อนกลับมาเริ่มรอบถัดไป

### 8.2 รีเซ็ต index ก่อนเรียก DLL

```csharp
calculatorIndex = index;
result += calculator.Calculate1(ref Data, ref calculatorIndex);
```

`Calculate1()` จะเพิ่มค่าของ `calculatorIndex` หลังคำนวณ เช่น ก่อนเรียกเป็น `100` และหลังเรียกเป็น `101`

แต่โปรแกรมต้องคำนวณ `Data[100]` ซ้ำในรอบต่อไป จึงกำหนด `calculatorIndex = index` ใหม่ก่อนเรียกทุกครั้ง

การรีเซ็ตนี้เปลี่ยนเฉพาะ index แต่ไม่คืนค่า `Data[index]` กลับเป็นค่าเดิม ดังนั้นค่าที่ถูกคูณด้วย `0.1` ในรอบก่อนหน้าจะถูกนำมาใช้ในรอบถัดไปตามอัลกอริทึมเดิม

## 9. เมธอดของ Worker Thread

```csharp
private static void DynamicWorker(object? state)
```

เมธอดนี้เป็นงานที่ส่งให้ thread ทุกตัว

### 9.1 รับหมายเลข worker

```csharp
int workerId = (int)state!;
```

ตอนเริ่ม thread โปรแกรมส่ง `workerId` เข้าไปด้วย `Start(workerId)` แต่ parameter ของ thread มีชนิด `object?` จึงต้องแปลงกลับเป็น `int`

เครื่องหมาย `!` บอก nullable analyzer ว่าโปรแกรมมั่นใจว่า `state` ไม่ใช่ `null`

### 9.2 สร้าง calculator และผลรวมส่วนตัว

```csharp
CalClass calculator = new();
decimal localResult = 0m;
int localValueCount = 0;
int localChunkCount = 0;
```

แต่ละ thread สร้าง `CalClass` ของตัวเอง จึงไม่แชร์ calculator object เดียวกัน และแต่ละ thread มี local variables ของตัวเอง ทำให้การบวกผลระหว่างทำงานไม่เกิด race condition

## 10. การรับ Chunk แบบ Atomic

```csharp
int startIndex =
    Interlocked.Add(ref nextChunkStart, ChunkSize) - ChunkSize;
```

สมมติค่าเริ่มต้นเป็น:

```text
nextChunkStart = 0
ChunkSize = 16,384
```

worker แรกเรียก `Interlocked.Add()` แล้วได้:

```text
nextChunkStart = 16,384
startIndex = 16,384 - 16,384 = 0
```

worker แรกจึงได้รับ index `0` ถึง `16,383` ส่วน worker ถัดไปจะได้รับ index `16,384` ถึง `32,767`

`Interlocked.Add()` ทำขั้นตอนอ่านและเพิ่มค่าเป็น atomic operation หมายความว่าไม่มี thread อื่นเข้ามาแทรกระหว่างสองขั้นตอนนี้ จึงไม่มีสอง workers ได้ `startIndex` เดียวกัน

หากเขียนแบบธรรมดา:

```csharp
startIndex = nextChunkStart;
nextChunkStart += ChunkSize;
```

สอง threads อาจอ่านค่าเดิมพร้อมกันและได้รับ chunk ซ้ำกันได้

`Interlocked` ไม่ใช่คำสั่งจัดการ thread สำเร็จรูปแบบ `Parallel.For` แต่เป็นเครื่องมือ synchronization สำหรับป้องกัน race condition ของตัวแปรที่แชร์กัน โปรแกรมยังคงสร้าง เริ่ม และรอ threads ด้วยตนเองทั้งหมด

## 11. การตรวจสอบขอบเขตของ Chunk

```csharp
if (startIndex >= CalculationCount)
{
    break;
}
```

หาก `startIndex` มากกว่าหรือเท่ากับ 10,000,000 แสดงว่าไม่มีงานเหลือ worker จึงออกจากลูปและจบการทำงาน

สำหรับ chunk สุดท้าย โปรแกรมใช้:

```csharp
int endIndex = Math.Min(startIndex + ChunkSize, CalculationCount);
```

เพื่อไม่ให้ `endIndex` เกินจำนวนข้อมูลที่ต้องคำนวณ

## 12. การประมวลผลข้อมูลภายใน Chunk

```csharp
for (int index = startIndex; index < endIndex; index++)
{
    localResult += CalculateValue(calculator, index);
}
```

worker วนผ่านทุก index ใน chunk ของตัวเอง แล้วเรียก `CalculateValue()` สำหรับแต่ละตำแหน่ง

เนื่องจากแต่ละ chunk ถูกแจกให้ worker เพียงตัวเดียว จึงไม่มีสอง threads แก้ `Data[index]` ตำแหน่งเดียวกันพร้อมกัน

เมื่อทำ chunk เสร็จ โปรแกรมบันทึกสถิติ:

```csharp
localValueCount += endIndex - startIndex;
localChunkCount++;
```

จากนั้น worker กลับไปขอ chunk ถัดไปจนกว่างานจะหมด

## 13. การบันทึกผลลัพธ์ของ Worker

```csharp
localResults[workerId] = localResult;
processedValues[workerId] = localValueCount;
processedChunks[workerId] = localChunkCount;
```

worker เขียนผลเพียงครั้งเดียวหลังทำงานทั้งหมดเสร็จ และเขียนลงตำแหน่งเฉพาะของตัวเอง จึงไม่ทับค่าของ worker อื่น

## 14. การอ่านข้อมูลจากไฟล์

```csharp
using FileStream stream =
    new("data.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
using BinaryReader reader = new(stream);
```

- เปิดไฟล์ `data.bin`
- เปิดเพื่ออ่านอย่างเดียว
- อนุญาตให้ process อื่นอ่านไฟล์พร้อมกันได้
- `using` ทำให้ stream และ reader ถูกปิดอัตโนมัติเมื่อออกจากเมธอด

ข้อมูลถูกอ่านด้วย:

```csharp
Data[index] = (decimal)(reader.ReadSingle() * 36.0f);
```

ขั้นตอนคือ:

1. อ่านค่า `float` ขนาด 32 บิตจากไฟล์
2. คูณด้วย `36.0f` ในรูปแบบ `float`
3. แปลงผลลัพธ์เป็น `decimal`
4. เก็บลงใน `Data[index]`

ลำดับนี้สำคัญ เพราะเหมือนกับโปรแกรมต้นฉบับ หากแปลงเป็น `decimal` ก่อนคูณ อาจทำให้รายละเอียดการปัดเศษเปลี่ยนและผลรวมสุดท้ายไม่ตรงกัน

## 15. การรับจำนวน Worker Threads

```csharp
int availableWorkers = Math.Max(1, Environment.ProcessorCount);
```

`Environment.ProcessorCount` คืนจำนวน logical processors ที่โปรแกรมสามารถใช้งานได้ ส่วน `Math.Max(1, ...)` รับประกันว่าค่าจะไม่น้อยกว่า 1

หากผู้ใช้กด Enter โดยไม่กรอกตัวเลข โปรแกรมจะใช้จำนวน logical processors ทั้งหมด:

```csharp
if (string.IsNullOrWhiteSpace(input))
{
    return availableWorkers;
}
```

ถ้าผู้ใช้กรอกตัวเลข โปรแกรมตรวจสอบว่าอยู่ระหว่าง `1` ถึงจำนวน processors ที่ตรวจพบ

## 16. การสร้างและเริ่ม Thread ด้วยตนเอง

โปรแกรมเตรียมอาร์เรย์สำหรับเก็บ threads:

```csharp
Thread[] threads = new Thread[workerCount];
```

จากนั้นสร้างและเริ่มแต่ละ thread:

```csharp
for (int workerId = 0; workerId < workerCount; workerId++)
{
    threads[workerId] = new Thread(DynamicWorker)
    {
        Name = $"Worker-{workerId + 1}"
    };

    threads[workerId].Start(workerId);
}
```

แต่ละรอบทำสามขั้นตอน:

1. สร้าง thread ด้วย `new Thread(DynamicWorker)`
2. ตั้งชื่อ เช่น `Worker-1`
3. เริ่มทำงานด้วย `Start(workerId)`

ส่วนนี้แสดงให้เห็นอย่างชัดเจนว่าโปรแกรมสร้างและจัดการ threads ด้วยตนเองตามข้อกำหนดของโจทย์

## 17. การรอทุก Thread ด้วย `Join()`

```csharp
foreach (Thread thread in threads)
{
    thread.Join();
}
```

`Join()` ทำให้ main thread รอจนกว่า worker ตัวนั้นจะทำงานเสร็จ

หากไม่มี `Join()` main thread อาจหยุดจับเวลาและรวมผลลัพธ์ก่อน workers ทำงานเสร็จ ทำให้เวลาและคำตอบไม่ถูกต้อง

## 18. การจับเวลา

โปรแกรมเริ่มจับเวลาก่อนเริ่ม workers:

```csharp
Stopwatch stopwatch = Stopwatch.StartNew();
```

และหยุดหลังจาก `Join()` ครบทุก thread:

```csharp
stopwatch.Stop();
```

เวลาที่วัดจึงรวม:

- เวลาเริ่ม threads
- เวลาแจก chunks
- เวลาคำนวณ
- เวลา synchronization
- เวลารอทุก worker ทำงานเสร็จ

เวลาอ่าน `data.bin` ไม่ถูกรวม เพราะโหลดข้อมูลก่อนเริ่ม `Stopwatch`

## 19. การรวมผลลัพธ์

หลังจากทุก worker ทำงานเสร็จ main thread รวมผลลัพธ์:

```csharp
decimal result = 0m;

for (int workerId = 0; workerId < workerCount; workerId++)
{
    result += localResults[workerId];
}
```

การรวมเกิดหลัง `Join()` ทุกตัว จึงไม่มี worker กำลังเขียน `localResults` ระหว่างที่ main thread อ่านข้อมูล

โปรแกรมยังรวม `processedValues` และ `processedChunks` เพื่อแสดงสถิติการกระจายงานของแต่ละ worker

## 20. การตรวจสอบความถูกต้อง

### 20.1 ตรวจจำนวนข้อมูล

```csharp
if (totalProcessedValues != CalculationCount)
{
    throw new InvalidOperationException(...);
}
```

เงื่อนไขนี้ตรวจว่าประมวลผลครบ 10,000,000 ค่า ไม่มีข้อมูลตกหล่นหรือถูกนับเกิน

### 20.2 ตรวจผลลัพธ์

```csharp
if (result != ExpectedResult)
{
    throw new InvalidOperationException(...);
}
```

ผลลัพธ์ต้องตรงกับ:

```text
4686980924312.00
```

หากผลไม่ตรง โปรแกรมจะแจ้งข้อผิดพลาดแทนการแสดงผลลัพธ์ที่ไม่ถูกต้องว่าเป็นผลสำเร็จ

## 21. การป้องกัน Race Condition

โปรแกรมป้องกัน race condition หลายระดับ:

| ทรัพยากร | วิธีป้องกัน |
|---|---|
| `nextChunkStart` | ใช้ `Interlocked.Add()` |
| `Data[index]` | แต่ละ chunk เป็นของ worker เพียงตัวเดียว |
| ผลรวมระหว่างคำนวณ | แต่ละ worker ใช้ `localResult` ของตัวเอง |
| `localResults` | แต่ละ worker เขียนคนละ index |
| การรวมผลสุดท้าย | main thread รวมหลัง `Join()` ครบทุกตัว |

หากหลาย threads ใช้ index หรือผลรวมเดียวกันโดยไม่มีการป้องกัน อาจเกิดปัญหา เช่น:

- คำนวณข้อมูลตำแหน่งเดียวกันซ้ำ
- ข้ามข้อมูลบางตำแหน่ง
- การบวกผลลัพธ์สูญหาย
- คำตอบเปลี่ยนไปในแต่ละครั้งที่รัน

## 22. เหตุผลที่เลือกชนิดตัวแปร

| การใช้งาน | ชนิด | เหตุผล |
|---|---|---|
| index และ counter ทั่วไป | `int` | ค่าสูงสุดประมาณ 11 ล้าน ซึ่งอยู่ในช่วงของ `int` และทำงานได้รวดเร็ว |
| ข้อมูลและผลรวม | `decimal` | ต้องตรงกับ DLL และรักษาผลลัพธ์ที่แน่นอน |
| index ที่ส่งเข้า DLL | `long` | signature ของ DLL กำหนดเป็น `ref long` |
| จำนวนรอบ | `int` | มีเพียง 30 รอบ |

`float` หรือ `double` อาจคำนวณเร็วกว่า `decimal` แต่ไม่สามารถรักษาผลลัพธ์เดียวกับ DLL ได้อย่างแน่นอน ดังนั้นโปรแกรมใช้ชนิดที่เร็วกว่าในตำแหน่งที่ปลอดภัย และเก็บ `decimal`/`long` ไว้เฉพาะจุดที่จำเป็นต่อความเข้ากันได้

## 23. เหตุผลที่โปรแกรมเร็วขึ้น

ความเร็วเพิ่มขึ้นจากหลายส่วนร่วมกัน:

1. ใช้หลาย threads คำนวณพร้อมกันบนหลาย logical processors
2. แบ่งงานเป็น chunks ขนาดเล็กเพื่อให้ worker ที่เร็วรับงานเพิ่มได้
3. ใช้ `Interlocked.Add()` แทน lock ขนาดใหญ่
4. แต่ละ worker มีผลรวมของตัวเอง จึงไม่ต้อง lock ทุกครั้งที่บวก
5. แต่ละ index ถูกแก้โดย worker เพียงตัวเดียว
6. หยุดรอบที่เหลือเมื่อค่าต่ำกว่า `5` และผลลัพธ์ต่อไปเป็นศูนย์
7. ใช้ `int` สำหรับ index และ counter ที่ไม่จำเป็นต้องเป็น `long`
8. รักษาการเรียก DLL เดิมเพื่อให้คำตอบถูกต้อง

## 24. วิธีทดลองเพื่อแสดงผลของ Multithreading

เพื่อพิสูจน์ว่าความเร็วเพิ่มขึ้นจาก threads ควรรันโปรแกรมหลายครั้งโดยใช้ข้อมูลชุดเดิมและเปรียบเทียบเวลา เช่น:

| จำนวน workers | เวลา (ms) | Speedup |
|---:|---:|---:|
| 1 | บันทึกจากการทดลอง | 1.00x |
| 2 | บันทึกจากการทดลอง | เวลา 1 thread ÷ เวลา 2 threads |
| 4 | บันทึกจากการทดลอง | เวลา 1 thread ÷ เวลา 4 threads |
| จำนวน logical processors ทั้งหมด | บันทึกจากการทดลอง | เวลา 1 thread ÷ เวลาหลาย threads |

สูตรคำนวณ speedup คือ:

```text
Speedup = เวลาที่ใช้ 1 thread ÷ เวลาที่ใช้หลาย threads
```

ควรทดลองแต่ละจำนวน threads อย่างน้อย 3 ครั้ง แล้วใช้ค่ากลางหรือค่าเฉลี่ย เพราะเวลาสามารถเปลี่ยนตามงานอื่นที่ระบบปฏิบัติการกำลังทำ อุณหภูมิ CPU และการจัดตารางเธรด

ทุกการทดลองต้องได้ผลลัพธ์เดียวกันคือ:

```text
4686980924312.00
```

## 25. สรุป

โปรแกรมนี้เพิ่มความเร็วด้วย manual multithreading โดยสร้าง `Thread` เอง แจกงานแบบ dynamic chunks และรอทุก worker ด้วย `Join()` จึงเป็นไปตามข้อกำหนดที่ห้ามใช้ `Parallel.For`

Dynamic scheduling ช่วยให้ CPU ที่มี P-core และ E-core กระจายงานตามความเร็วจริงของแต่ละ worker ขณะที่ `Interlocked.Add()` และผลรวมแยกตาม thread ทำให้โปรแกรม thread-safe

สุดท้าย โปรแกรมตรวจทั้งจำนวนข้อมูลและผลลัพธ์ เพื่อรับประกันว่าการเพิ่มความเร็วไม่ทำให้ความถูกต้องเปลี่ยนไป
