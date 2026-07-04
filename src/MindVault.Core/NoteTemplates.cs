namespace MindVault.Core;

public static class NoteTemplates
{
    public static string Project(string name, string date) =>
        $"""
        ---
        type: project
        status: active
        created: {date}
        updated: {date}
        tags:
          - project
        links: []
        ---

        # {name}

        ## Goal

        ## Non-Negotiables

        ## Architecture

        ## Active Work

        ## Open Questions

        ## Decisions

        ## Risks

        ## Next Actions

        """;

    public static string Decision(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: decision
        status: accepted
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - decision
        links:
          - "[[{projectStem}]]"
        ---

        # Decision: {title}

        ## Context

        ## Decision

        ## Rejected Alternatives

        ## Reasoning

        ## Consequences

        ## Reversal Conditions

        ## Links

        """;

    public static string Task(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: task
        status: open
        created: {date}
        updated: {date}
        project: {projectTitle}
        priority: medium
        tags:
          - task
        links:
          - "[[{projectStem}]]"
        ---

        # Task: {title}

        ## Description

        ## Acceptance Criteria

        ## Context

        ## Links

        ## Status Notes

        """;

    public static string Risk(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: risk
        status: open
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - risk
        links:
          - "[[{projectStem}]]"
        ---

        # Risk: {title}

        ## Risk

        ## Impact

        ## Likelihood

        ## Mitigation

        ## Owner/Area

        ## Status Notes

        """;

    public static string Constraint(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: constraint
        status: active
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - constraint
        links:
          - "[[{projectStem}]]"
        ---

        # Constraint: {title}

        ## Constraint

        ## Why It Exists

        ## Consequences If Broken

        """;

    public static string Architecture(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: architecture
        status: active
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - architecture
        links:
          - "[[{projectStem}]]"
        ---

        # Architecture: {title}

        ## Overview

        ## Components

        ## Data Flow

        ## Key Decisions

        ## Known Trade-offs

        """;

    public static string ImplementationLog(string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: memory
        status: active
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - memory
          - log
        links:
          - "[[{projectStem}]]"
        ---

        # Log: {projectTitle}

        ## Sessions

        """;

    public static string Review(string title, string projectTitle, string projectStem, string date) =>
        $"""
        ---
        type: review
        status: open
        created: {date}
        updated: {date}
        project: {projectTitle}
        tags:
          - review
        links:
          - "[[{projectStem}]]"
        ---

        # Review: {title}

        ## Summary

        ## Critical Issues

        ## Important Improvements

        ## Minor Cleanup

        ## Risks

        ## Recommended Next Actions

        """;

    public static string Prompt(string title, string date) =>
        $"""
        ---
        type: prompt
        status: active
        created: {date}
        updated: {date}
        tags:
          - prompt
        links: []
        ---

        # Prompt: {title}

        ## Purpose

        ## Prompt

        ## Usage Notes

        """;

    public static string Memory(string title, string date) =>
        $"""
        ---
        type: memory
        status: active
        created: {date}
        updated: {date}
        tags:
          - memory
        links: []
        ---

        # Memory: {title}

        ## Fact

        ## Why It Matters

        ## Source

        """;

    /// <summary>Template files written into 08_Templates by `init` (placeholder names, empty dates).</summary>
    public static IEnumerable<(string RelativePath, string Content)> InitTemplates()
    {
        yield return ("08_Templates/Project.md", Project("Project Name", ""));
        yield return ("08_Templates/Decision.md", Decision("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Task.md", Task("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Risk.md", Risk("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Constraint.md", Constraint("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Architecture.md", Architecture("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Implementation Log.md", ImplementationLog("Project Name", "Project Name", ""));
        yield return ("08_Templates/Review.md", Review("Title", "Project Name", "Project Name", ""));
        yield return ("08_Templates/Prompt.md", Prompt("Title", ""));
        yield return ("08_Templates/Memory.md", Memory("Title", ""));
    }
}
