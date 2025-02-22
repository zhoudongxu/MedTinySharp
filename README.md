[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
![Build Status](https://github.com/microsoft/Zen/actions/workflows/dotnet.yml/badge.svg)
![badge](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/rabeckett/6623db8f2d0c01f6b2bc880e6219f97f/raw/code-coverage.json)

# Introduction
MedTinySharp is a symbolic model-checking library for C# built on top of Zen, extending C# with MedTiny syntax to facilitate the modeling of distributed algorithms directly in C#. This library provides the following key contributions:

- Transforms hierarchical automata into a standard labeled transition system and integrates LTL (Linear Temporal Logic) for specifying properties.
- Supports multiple symbolic model-checking backends, including a bounded model-checking (BMC) algorithm that unrolls transition loops into Boolean formulas for analysis using the Z3 solver.
- Encodes nearly all C# primitive types and commonly used data structures (e.g., sets) into Z3 expressions, maintaining full compatibility with native C#.
MedTinySharp is a true C# library, allowing developers to write and verify distributed algorithms entirely within standard C#.
# Table of contents
- [Introduction](#introduction)
- [Table of contents](#table-of-contents)
- [Installation](#installation)
- [Overview ](#overview-of-zen)
  - [System Architecture](#zen-expressions)
  - [Executing a function](#executing-a-function)
  - [Searching for inputs](#searching-for-inputs)
  - [Computing with sets](#computing-with-sets)
  - [Generating test inputs](#generating-test-inputs)
  - [Optimization](#optimization)
- [Supported data types](#supported-data-types)
  - [Primitive types](#primitive-types)
  - [Integer types](#integer-types)
  - [Options, Tuples](#options-tuples)
  - [Real Values](#real-values)
  - [Finite Sequences, Bags, Maps](#finite-sequences-bags-maps)
  - [Unbounded Sets and Maps](#unbounded-sets-and-maps)
  - [Constant Sets and Maps](#constant-sets-and-maps)
  - [Fixed Length Arrays](#fixed-length-arrays)
  - [Sequences, Strings, and Regular Expressions](#sequences-strings-and-regular-expressions)
  - [Custom classes and structs](#custom-classes-and-structs)
  - [Enumerated values](#enumerated-values)


<a name="installation"></a>
# Installation
Just add the project to your visual studio solution. 

<a name="overview-of-zen"></a>
# Overview 
<a name="System Architecture"></a>
## System Architecture
![架构图](/架构图.png)

The input to the project is the MedtinySharp program, which is first transformed into a Label Transition System (LTS) using the Roslyn API and the Automaton Flattening algorithm. During the LTS verification process, we employ several advanced model checking techniques, including Bounded Model Checking (BMC) and Incremental Model Checking (IC3). The implementation of these verification algorithms relies on the Z3 solver to efficiently handle the logical constraints involved in the verification process. Based on the verification results, the system outputs either a satisfiable (SAT) result or a counterexample, providing an in-depth analysis of the program's behavior.


<a name="computing-with-zen-expressions"></a>
## Zen Expressions

`Zen<T>` objects are just normal .NET objects, we can pass them and return them from functions. For instance, the following code computes a new symbolic integer from two integer inputs `x` and `y`:

```csharp
Zen<int> MultiplyAndAdd(Zen<int> x, Zen<int> y)
{
    return 3 * x + y;
}
```

Zen overloads common C# operators such as `&,|,^,<=, <, >, >=, +, -, *, true, false` to work with Zen values and supports implicit conversions to lift C# values (of type `T`) to Zen values (of type `Zen<T>`). Zen can represent a "function" like the one above to perform various symbolic tasks by creating a `ZenFunction` to wrap the `MultiplyAndAdd` function:

```csharp
var function = new ZenFunction<int, int, int>(MultiplyAndAdd);
```

<a name="executing-a-function"></a>
## Executing a function

Zen can execute the function we have built on inputs by calling the `Evaluate` method on the `ZenFunction`:

```csharp
var output = function.Evaluate(3, 2); // output = 11
```

This will interpret the expression tree created by the Zen function at runtime and return back a C# `int` value in this case. Of course interpreting a tree is quite slow compared to multiplying a few numbers, so if you need to execute a function many times, Zen can compile the model using the C# `System.Reflection.Emit` API. This generates IL instructions that execute efficiently - as if the function had been written using actual `int` values. Doing so is easy, just call the `Compile` method on the function first:

```csharp
function.Compile();
output = function.Evaluate(3, 2); // output = 11
```

Or alternatively:

```csharp
Func<int, int, int> f = Zen.Compile(MultiplyAndAdd);
var output = f(3, 2); // output = 11
```

We can see the difference by comparing the performance between the two:

```csharp
var watch = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < 1000000; i++)
    function.Evaluate(3, 2);

Console.WriteLine($"interpreted function time: {watch.ElapsedMilliseconds}ms");
watch.Restart();

function.Compile();

Console.WriteLine($"compilation time: {watch.ElapsedMilliseconds}ms");
watch.Restart();

for (int i = 0; i < 1000000; i++)
    function.Evaluate(3, 2);

Console.WriteLine($"compiled function time: {watch.ElapsedMilliseconds}ms");
```

```text
interpreted function time: 4601ms
compilation time: 4ms
compiled function time: 2ms
```


<a name="searching-for-inputs"></a>
## Searching for inputs

Zen can find function inputs that lead to some (un)desirable outcome. For example, we can find an `(x, y)` input pair such that `x` is less than zero and the output of the function is `11`:

```csharp
var input = function.Find((x, y, result) => Zen.And(x <= 0, result == 11)); 
// input.Value = (-1883171776, 1354548043)
```

The type of the result in this case is `Option<(int, int)>`, which will have a pair of integer inputs that make the output 11 if such a pair exists. In this case the library will find `x = -1883171776` and `y = 1354548043`

To find multiple inputs, Zen supports an equivalent `FindAll` method, which returns an `IEnumerable` of inputs where each input in `inputs` will be unique so there are no duplicates.

```csharp
using System.Linq;
...
var inputs = function.FindAll((x, y, result) => Zen.And(x <= 0, result == 11)).Take(5);
```


<a name="computing-with-sets"></a>
## Computing with sets

While the `Find` function provides a way to find a single input to a function, Zen also provides an additional API for reasoning about sets of inputs and outputs to functions. It does this through a `StateSetTransformer` API. A transformer is created by calling the `Transformer()` method on a `ZenFunction` (or by calling `Zen.Transformer(...)`):

```csharp
var f = new ZenFunction<uint, uint>(i => i + 1);
StateSetTransformer<uint, uint> t = f.Transformer();
```

Transformers allow for manipulating (potentially huge) sets of objects efficient. For example, we can get the set of all input `uint` values where adding one will result in an output `y` that is no more than 10 thousand:

```csharp
StateSet<uint> inputSet = t.InputSet((x, y) => y <= 10000);
```

This set will include all the values `0 - 9999` as well as `uint.MaxValue` due to wrapping. Transformers can also manpulate sets by propagating them forward or backwards: 

```csharp
StateSet<uint> outputSet = t.TransformForward(inputSet);
```

Finally, `StateSet` objects can also be intersected, unioned, and negated. We can pull an example element out of a set as follows (if one exists):

```csharp
Option<uint> example = inputSet.Element(); // example.Value = 0
```

Internally, transformers leverage [binary decision diagrams](https://github.com/microsoft/DecisionDiagrams) to represent, possibly very large, sets of objects efficiently.


<a name="generating-test-inputs"></a>
## Generating test inputs

Zen can automatically generate test inputs for a given model by finding inputs that will lead to different execution paths. For instance, consider an insertion sort implementation. We can ask Zen to generate test inputs for the function that can then be used, for instance to test other sorting algorithms:

```csharp
var f = new ZenFunction<Pair<int, int>, int>(pair => Zen.If<int>(pair.Item1() < pair.Item2(), 1, 2));

foreach (var input in f.GenerateInputs())
{
    Console.WriteLine($"input: {input}");
}
```

In this case, we get the following output:

```text
input: (0, 0)
input: (0, 1)
```

The test generation approach uses [symbolic execution](https://en.wikipedia.org/wiki/Symbolic_execution) to enumerate program paths and solve constraints on inputs that lead down each path. Each `Zen.If` expression is treated as a program branch point (note: you can set the setting `Settings.PreserveBranches = true` to prevent Zen from simplifying formulas involving `If` by default if you want to preserve the expression structure.).

<a name="optimization"></a>
## Optimization

Zen supports optimization of objective functions subject to constraints. The API is similar to that for `Solve`, but requires a maximization or minimization objective. The solver will find the maximal satisfying assignment to the variables.

```csharp
var a = Zen.Symbolic<Real>();
var b = Zen.Symbolic<Real>();
var constraints = Zen.And(a <= (Real)10, b <= (Real)10, a + (Real)4 <= b);
var solution = Zen.Maximize(objective: a + b, subjectTo: constraints); // a = 6, b = 10
```


<a name="supported-data-types"></a>
# Supported data types

Zen currently supports a subset of .NET types and also introduces some of its own data types summarized below.

| .NET Type   | Description          | Supported by Z3 backend | Supported by BDD backend | Supported by `StateSetTransformers`
| ------ | -------------------- | ----------------------- | ------------------------ | ------------|
| `bool`   | {true, false}        | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `byte`   | 8-bit value          | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `char`   | 16-bit UTF-16 character   | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `short`  | 16-bit signed value  | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `ushort` | 16-bit unsigned value| :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `int`    | 32-bit signed value  | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `uint`   | 32-bit unsigned value| :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `long`   | 64-bit signed value  | :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `ulong`  | 64-bit unsigned value| :heavy_check_mark:      | :heavy_check_mark:       | :heavy_check_mark: |
| `Int<_N>` | N-bit signed value| :heavy_check_mark:      | :heavy_check_mark:  | :heavy_check_mark: |
| `UInt<_N>` | N-bit unsigned value| :heavy_check_mark: | :heavy_check_mark:  | :heavy_check_mark: |
| `Option<T>`    | an optional/nullable value of type `T` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark:  |
| `Pair<T1, ...>`  | pairs of different values | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark:  |
| `class`, `struct` | classes and structs with public fields and/or properties | :heavy_check_mark: | :heavy_check_mark:  | :heavy_check_mark:  |
| `FSeq<T>`       | finite length sequence of elements of type `T` | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `FSet<T>`       | finite size set of elements of type `T` | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `FString` | finite length string | :heavy_check_mark: | :heavy_minus_sign:  | :heavy_minus_sign:  |
| `BigInteger` | arbitrary length integer| :heavy_check_mark:           | :heavy_minus_sign:                 | :heavy_minus_sign:  |
| `Real` | arbitrary precision rational number | :heavy_check_mark:           | :heavy_minus_sign:                 | :heavy_minus_sign:  |
| `Map<T1, T2>` | arbitrary size maps of keys and values of type `T1` and `T2`. Note that `T1` and `T2` can not use finite sequences | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `Set<T>` | arbitrary size sets of values of type `T`. Same restrictions as with `Map<T1, T2>` | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `CMap<T1, T2>` | maps of constant keys of type `T1` to values of type `T2`. | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `CSet<T>` | sets of constants of type `T`. | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `Array<T, _N>` | Fixed size arrays of values of type `T`. | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `Seq<T>` | arbitrary size sequences of values of type `T`. Same restrictions as with `Set<T>`. Note that SMT solvers use heuristics to solve for sequences and are incomplete. | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |
| `string` | arbitrary size strings. Implemented as `Seq<char>` | :heavy_check_mark: | :heavy_minus_sign: | :heavy_minus_sign:  |


<a name="primitive-types"></a>
## Primitive types

Zen supports the primitive types `bool, byte, char, short, ushort, int, uint, long, ulong`. All primitive types support (in)equality and integer types support integer arithmetic operations. As an example:

```csharp
var x = Symbolic<int>();
var y = Symbolic<int>();
var c1 = (~x & y) == 1;
var c2 = And(x + y > 0, x + y < 100);
var solution = And(c1, c2).Solve(); // x = -20, y = 105
```

<a name="integer-types"></a>
## Integer types

Aside from primitive types, Zen also supports the `BigInteger` type found in `System.Numerics` for reasoning about ubounded integers as well as other types of integers with fixed, but non-standard bit width (for instance a 7-bit integer). Out of the box, Zen provides the types `Int<_N>` and `UInt<_N>` for `N`=1, 2, 3, ..., 99, 100, 128, 256, 512 ,1024. You can also create a custom integer size by simply declaring a new struct:

```csharp
public struct _101 { }
```

<a name="options-and-tuples"></a>
## Options, Tuples

Zen offers `Pair<T1, T2, ...>`, types as a lightweight alternative to classes. By default all values are assumed to be non-null by Zen. For nullable values, it provides an `Option<T>` type.

```csharp
var b = Symbolic<Option<byte>>();
var p = Symbolic<Pair<int, int>>>();
var solution = And(b.IsNone(), p.Item1() == 3).Solve(); // b = None, p = (3, 0)
```

<a name="real-values"></a>
## Real Values

Zen supports arbitrary precision rational numbers through the `Real` type.

```csharp
var c = new Real(3, 2); // the fraction 3/2 or equivalently 1.5 
var x = Symbolic<Real>();
var y = Symbolic<Real>();
var solution = (2 * x + 3 * y == c).Solve(); // x = 1/2, y = 1/6
```

<a name="finite-sequences-bags-maps"></a>
## Finite Sequences, Bags, Maps

Zen supports several high-level data types that are finite (bounded) in size (the default size is 5 but can be changed). These include:

- `FSeq<T>` for reasoning about variable length sequences of values where the order is important.
- `FSet<T>` represents finite sets.

One can implement complex functionality over `FSeq<T>` types by combining the elements of the sequence. For instance, we can sum the elements of a sequence:

```csharp
public Zen<int> Sum<T>(Zen<FSeq<int>> seq)
{
    return seq.Fold(Zen.Constant(0), (x, y) => x + y);
}
```


<a name="unbounded-sets-maps"></a>
## Unbounded Sets and Maps

Zen supports `Set<T>` and `Map<T1, T2>` data types that do not restrict the size of the set/map. This type only works with the Z3 backend and requires that `T`, `T1` and `T2` not contain any finitized types (`FSeq`, `FString`, or `FSet`). Primitive types (bool, integers, string, BigInteger), classes/structs are allowed.

```csharp
var s  = Symbolic<string>();
var s1 = Symbolic<Set<string>>();
var s2 = Symbolic<Set<string>>();
var s3 = Symbolic<Set<string>>();
var s4 = Symbolic<Set<string>>();

var c1 = s1.Contains("a");
var c2 = s1.Intersect(s2).Contains("b");
var c3 = Implies(s == "c", s3.Add(s) == s2);
var c4 = s4 == s1.Union(s2);
var solution = And(c1, c2, c3, c4).Solve(); // s = "a", s1 = {b, a}, s2 = {b}, s3 = {}, s4 = {b, a}
```

<a name="constant-sets-maps"></a>
## Constant Sets and Maps

Arbitrary sets and maps described above are compiled to the SMT solver theory of Arrays. While this theory is quite general, it has known performance limitations. As a lightweight alternative, Zen provides the `CMap<T1, T2>` and `CSet<T>` classes that offer similar APIs but with the restriction that any map keys or set elements must be C# constant values and not Zen expressions. Zen will compile these sets and maps by creating fresh variables for all possible constants used by the user for these types.

Constant maps are useful for managing a finite number of unknown variables that should be indexed to some data (e.g., a symbolic boolean variable for every edge in a C# graph), and may have better performance in many cases.

`CMap<T1, T2>` represents a total map from keys of type `T1` to values of type `T2`. When a key is not explicitly added to the map, the resulting value will be the Zen default value for the type `T2` (e.g., `0` for integers, `false` for booleans). `CSet<T>` is simply implemented as a `CMap<T, bool>` that says for each key, if the element is in the set. Any example use is shown below:


```csharp
var x = Symbolic<int>();
var m1 = Symbolic<CMap<string, int>>();
var m2 = Symbolic<CMap<string, int>>();

var c1 = m1.Get("a") == Zen.If(x < 10, x + 1, x + 2);
var c2 = m2 == m1.Set("b", x);
var solution = And(c1, c2).Solve(); // x = 0, m1 = m2 = {"a" => 1, _ => 0}
```

Constant maps and sets have several limitations:
* Inequality may not always give the expected result, as the constant maps do not have a canonical representation.
* They can not be used as values in the `Map`, `Set`, or `Seq` types. This restriction may be relaxed in the future.

<a name="arrays"></a>
## Fixed Length Arrays

Zen can model fixed-length arrays of symbolic values using the `Array<T, TSize>` class. As an example:

```csharp
var a = Zen.Symbolic<Array<int, _10>>();           // create a symbolic array of size 10
Zen<int>[] elements = a.ToArray();                 // get the symbolic elements of the array
var solution = Zen.And(
    elements.Aggregate(Zen.Plus) == 100,
    a.All(x => Zen.And(x >= 1, x <= 20))).Solve(); // a = [8,6,13,16,14,15,5,13,5,5]
```

The type parameter `TSize` specifies the size of the array. The types `_1` through `_100` are predefined in the library. To add a custom size, you can create a new struct following this naming convention:

```csharp
struct _150 { }
```


<a name="strings-and-sequences"></a>
## Sequences, Strings, and Regular Expressions

Zen has a `Seq<T>` type to represent arbitrarily large sequences of elements of type `T`. As there is no complete decision procedure for sequences in constraint solvers, queries for sequences may not always terminate, and you may need to use a timeout. If this is not acceptable, you can always use `FSeq` or `FString` instead, which will model a finite sequence up to a given size. Sequences also support matching against regular expressions. As an example:

```csharp
Regex<int> r = Regex.Star(Regex.Char(1)); // zero or more 1s in a Seq<int>

var s1 = Symbolic<Seq<int>>();
var s2 = Symbolic<Seq<int>>();

var c1 = s1.MatchesRegex(r);
var c2 = s1 != Seq.Empty<int>();
var c3 = Not(s2.MatchesRegex(r));
var c4 = s1.Length() == s2.Length();
var solution = And(c1, c2, c3, c4).Solve(); // s1 = [1], s2 = [0]
```

Zen supports the `string` type for reasoning about unbounded strings (the `string` type is implemented as a `Seq<char>`). Strings also support matching regular expressions. Zen supports a limited subset of regex constructs currently - it supports anchors like `$` and `^` but not any other metacharacters like `\w,\s,\d,\D,\b` or backreferences `\1`. As an example:

```csharp
Regex<char> r1 = Regex.Parse("[0-9a-z]+");
Regex<char> r2 = Regex.Parse("(0.)*");

var s = Symbolic<string>();

var c1 = s.MatchesRegex(Regex.Intersect(r1, r2));
var c2 = s.Contains("a0b0c");
var c3 = s.Length() == new BigInteger(10);
var solution = And(c1, c2, c3).Solve(); // s = "020z0a0b0c"
```

<a name="custom-classes-and-structs"></a>
## Custom classes and structs

Zen supports custom `class` and `struct` types with some limitations. It will attempt to model all public fields and properties. For these types to work, either (1) the class/struct must also have a default constructor and all properties must be allowed to be set, or (2) there must be a constructor with matching parameter names and types for all the public fields. For example, the following are examples that are and are not allowed:

```csharp
// this will work because the fields are public
public class Point 
{ 
    public int X;
    public int Y;
}

// this will work because the properties are public and can be set.
public class Point 
{ 
    public int X { get; set; }
    public int Y { get; set; }
}

// this will NOT work because X can not be set.
public class Point 
{ 
    public int X { get; }
    public int Y { get; set; }
}

// this will work as well since there is a constructor with the same parameter names.
// note that _z will not be modeled by Zen.
public class Point 
{ 
    public int X { get; }
    public int Y { get; set; }
    private int _z;

    public Point(int x, int y) 
    {
        this.X = x;
        this.Y = y;
    }
}

```

<a name="enums"></a>
## Enumerated values

Enums in C# are just structs that wrap some backing type. Zen will model enums like any other struct. For example, Zen will model the following enum as a byte:

```csharp
public enum Origin : byte
{
    Egp,
    Igp,
    Incomplete,
}
```

By default, Zen does not constraint an enum value to only be one of the enumerated values - it can be any value allowed by the backing type (any value between 0 and 255 in this example instead of just the 3 listed). If you want to add a constraint to ensure the value is only one of those enumerated by the user, you write a function like the following to test if a value is one of those expected:

```csharp
public Zen<bool> IsValidOrigin(Zen<Origin> origin)
{
    return Zen.Or(Enum.GetValues<Origin>().Select(x => origin == x));
}
```


<a name="zen-attributes"></a>
# Zen Attributes

Zen provides two attributes to simplify the creation and manipulation of symbolic objects. The first attribute `[ZenObject]` can be applied to classes or structs. It uses C# source generators to generate Get and With methods for all public fields and properties.

```csharp
[ZenObject]
public class Point 
{ 
    public int X { get; set; }
    public int Y { get; set; }

    public static Zen<Point> Add(Zen<Point> p1, Zen<Point> p2)
    {
        return p1.WithX(p1.GetX() + p2.GetX()).WithY(p1.GetY() + p2.GetY());
    }
}
```

Note that this requires C# 9.0 and .NET 6 or later to work. In addition, you must add the ZenLib.Generators nuget package to enable code generation. The other attribute supported is the `ZenSize` attribute, which controls the size of a generated field in an object. For example, to fix the size of a `FSeq` to 10:

```csharp
public class Person
{
    [ZenSize(depth: 10)]
    public FSeq<string> Contacts { get; set; }
}
```
