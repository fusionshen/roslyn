﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes.Suppression;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.UnitTests.Diagnostics;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.Diagnostics
{
    public abstract class AbstractSuppressionDiagnosticTest : AbstractUserDiagnosticTest
    {
        protected abstract int CodeActionIndex { get; }
        protected virtual bool IncludeSuppressedDiagnostics => false;
        protected virtual bool IncludeUnsuppressedDiagnostics => true;
        protected virtual bool IncludeNoLocationDiagnostics => true;

        protected Task TestAsync(string initial, string expected)
        {
            return TestAsync(initial, expected, parseOptions: null, index: CodeActionIndex);
        }

        internal abstract Tuple<DiagnosticAnalyzer, ISuppressionFixProvider> CreateDiagnosticProviderAndFixer(Workspace workspace);

        private ImmutableArray<Diagnostic> FilterDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            if (!IncludeNoLocationDiagnostics)
            {
                diagnostics = diagnostics.Where(d => d.Location.IsInSource);
            }

            if (!IncludeSuppressedDiagnostics)
            {
                diagnostics = diagnostics.Where(d => !d.IsSuppressed);
            }

            if (!IncludeUnsuppressedDiagnostics)
            {
                diagnostics = diagnostics.Where(d => d.IsSuppressed);
            }

            return diagnostics.ToImmutableArray();
        }

        internal override async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(
            TestWorkspace workspace, TestParameters parameters)
        {
            var providerAndFixer = CreateDiagnosticProviderAndFixer(workspace);

            var provider = providerAndFixer.Item1;
            var document = GetDocumentAndSelectSpan(workspace, out var span);
            var diagnostics = await DiagnosticProviderTestUtilities.GetAllDiagnosticsAsync(provider, document, span);
            return FilterDiagnostics(diagnostics);
        }

        internal override async Task<(ImmutableArray<Diagnostic>, ImmutableArray<CodeAction>, CodeAction actionToInvoke)> GetDiagnosticAndFixesAsync(
            TestWorkspace workspace, TestParameters parameters)
        {
            var providerAndFixer = CreateDiagnosticProviderAndFixer(workspace);

            var provider = providerAndFixer.Item1;
            string annotation = null;
            if (!TryGetDocumentAndSelectSpan(workspace, out var document, out var span))
            {
                document = GetDocumentAndAnnotatedSpan(workspace, out annotation, out span);
            }

            var testDriver = new TestDiagnosticAnalyzerDriver(document.Project, provider, includeSuppressedDiagnostics: IncludeSuppressedDiagnostics);
            var fixer = providerAndFixer.Item2;
            var diagnostics = (await testDriver.GetAllDiagnosticsAsync(provider, document, span))
                .Where(d => fixer.CanBeSuppressedOrUnsuppressed(d));

            var filteredDiagnostics = FilterDiagnostics(diagnostics);

            var wrapperCodeFixer = new WrapperCodeFixProvider(fixer, filteredDiagnostics.Select(d => d.Id));
            return await GetDiagnosticAndFixesAsync(
                filteredDiagnostics, provider, wrapperCodeFixer, testDriver, 
                document, span, annotation, parameters.index);
        }
    }
}
