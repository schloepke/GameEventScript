// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Tool;

namespace GameEventScript.Tests.Native.Tool;

/// <summary>Verifies command history shared between ordinary and timer-aware input.</summary>
[TestClass]
public sealed class GameEventScriptRunHistoryTests
{
    /// <summary>Verifies history survives reader changes and restores the current multiline draft.</summary>
    [TestMethod]
    public void NavigationSurvivesReaderChangesAndPreservesDraft()
    {
        var history = new RunHistory();
        history.Add("first");
        history.Add("emit after 1s Ping()");
        history.Begin();
        Assert.AreEqual("emit after 1s Ping()", history.Move("let x be 1\nemit Ping(x)", true));
        Assert.AreEqual("first", history.Move("ignored", true));
        Assert.AreEqual("first", history.Move("ignored", true));
        Assert.AreEqual("emit after 1s Ping()", history.Move("ignored", false));
        Assert.AreEqual("let x be 1\nemit Ping(x)", history.Move("ignored", false));
        history.Add("while waiting");
        history.Begin();
        Assert.AreEqual("while waiting", history.Move("", true));
        Assert.AreEqual("emit after 1s Ping()", history.Move("", true));
    }

    /// <summary>Verifies empty history and duplicate submissions do not lose the editable draft.</summary>
    [TestMethod]
    public void EmptyHistoryAndDuplicatesPreserveDraft()
    {
        var history = new RunHistory();
        history.Begin();
        Assert.AreEqual("draft", history.Move("draft", true));
        history.Add("first");
        history.Add("first");
        history.Add(" ");
        history.Begin();
        Assert.AreEqual("first", history.Move("draft", true));
        Assert.AreEqual("first", history.Move("first", true));
        Assert.AreEqual("draft", history.Move("first", false));
    }
}
