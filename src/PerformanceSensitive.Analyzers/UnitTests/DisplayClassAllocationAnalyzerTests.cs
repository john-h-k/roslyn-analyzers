﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PerformanceSensitive.CSharp.Analyzers;
using Xunit;

namespace PerformanceSensitive.Analyzers.UnitTests
{
    internal class DisplayClassAllocationAnalyzerTests : AllocationAnalyzerTestsBase
    {
        [Fact]
        public void DisplayClassAllocation_AnonymousMethodExpressionSyntax()
        {
            var sampleProgram =
@"using System;

class Test
{
    static void Main()
    {
        Action action = CreateAction<int>(5);
    }

    static Action CreateAction<T>(T item)
    {
        T test = default(T);
        int counter = 0;
        return delegate
        {
            counter++;
            Console.WriteLine(""counter={0}"", counter);
        };
    }
}";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.Equal(3, info.Allocations.Length);
            // Diagnostic: (14,16): warning HeapAnalyzerLambdaInGenericMethodRule: Considering moving this out of the generic method
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.LambaOrAnonymousMethodInGenericMethodRule.Id, line: 14, character: 16);
            // Diagnostic: (13,13): warning HeapAnalyzerClosureCaptureRule: The compiler will emit a class that will hold this as a field to allow capturing of this closure
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 13, character: 13);
            // Diagnostic: (14,16): warning HeapAnalyzerClosureSourceRule: Heap allocation of closure Captures: counter
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureDriverRule.Id, line: 14, character: 16);
        }

        [Fact]
        public void DisplayClassAllocation_SimpleLambdaExpressionSyntax()
        {
            var sampleProgram =
@"using System.Collections.Generic;
using System;
using System.Linq;

public class Testing<T>
{
    public Testing()
    {
        int[] intData = new[] { 123, 32, 4 };
        int min = 31;
        var results = intData.Where(i => i > min).ToList();
    }
}";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.SimpleLambdaExpression));

            Assert.Equal(2, info.Allocations.Length);
            // Diagnostic: (10,13): warning HeapAnalyzerClosureCaptureRule: The compiler will emit a class that will hold this as a field to allow capturing of this closure
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 10, character: 13);
            // Diagnostic: (11,39): warning HeapAnalyzerClosureSourceRule: Heap allocation of closure Captures: min
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureDriverRule.Id, line: 11, character: 39);
        }

        [Fact]
        public void DisplayClassAllocation_ParenthesizedLambdaExpressionSyntax()
        {
            var sampleProgram =
@"using System.Collections.Generic;
using System;
using System.Linq;

var words = new[] { ""foo"", ""bar"", ""baz"", ""beer"" };
var actions = new List<Action>();
foreach (string word in words) // <-- captured closure
{
    actions.Add(() => Console.WriteLine(word)); // <-- reason for closure capture
}";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression));

            Assert.Equal(2, info.Allocations.Length);
            // Diagnostic: (7,17): warning HeapAnalyzerClosureCaptureRule: The compiler will emit a class that will hold this as a field to allow capturing of this closure
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 7, character: 17);
            // Diagnostic: (9,20): warning HeapAnalyzerClosureSourceRule: Heap allocation of closure Captures: word
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureDriverRule.Id, line: 9, character: 20);
        }

        [Fact]
        public void DisplayClassAllocation_DoNotReportForNonCapturingAnonymousMethod()
        {
            var snippet = @"
                public static void Sorter(int[] arr) {
                    System.Array.Sort(arr, delegate(int x, int y) { return x - y; });
                }";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, snippet, ImmutableArray.Create<SyntaxKind>());
            Assert.Empty(info.Allocations);
        }

        [Fact]
        public void DisplayClassAllocation_DoNotReportForNonCapturingLambda()
        {
            var snippet = @"
                public void Sorter(int[] arr) {
                    System.Array.Sort(arr, (x, y) => x - y);
                }";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, snippet, ImmutableArray.Create<SyntaxKind>());
            Assert.Empty(info.Allocations);
        }

        [Fact]
        public void DisplayClassAllocation_ReportForCapturingAnonymousMethod()
        {
            var snippet = @"
                public void Sorter(int[] arr) {
                    int z = 2;
                    System.Array.Sort(arr, delegate(int x, int y) { return x - z; });
                }";

            var analyser = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyser, snippet, ImmutableArray.Create<SyntaxKind>());
            Assert.Equal(2, info.Allocations.Length);
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 3, character: 25);
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureDriverRule.Id, line: 4, character: 44);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            throw new System.NotImplementedException();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            throw new System.NotImplementedException();
        }
    }
}
