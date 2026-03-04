using FluentAssertions;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Domain.Tests;

public class EnumTests
{
    [Fact]
    public void SkillLevel_HasCorrectValues()
    {
        ((int)SkillLevel.Expert).Should().Be(1);
        ((int)SkillLevel.Experienced).Should().Be(2);
        ((int)SkillLevel.Beginner).Should().Be(3);
        ((int)SkillLevel.Cannot).Should().Be(4);
    }

    [Fact]
    public void BoardPosition_HasCorrectValues()
    {
        ((int)BoardPosition.Left).Should().Be(1);
        ((int)BoardPosition.Right).Should().Be(2);
    }

    [Fact]
    public void OptimizationStrategy_HasThreeValues()
    {
        Enum.GetValues<OptimizationStrategy>().Should().HaveCount(3);
    }

    [Fact]
    public void SolverStatus_HasFourValues()
    {
        Enum.GetValues<SolverStatus>().Should().HaveCount(4);
    }

    [Fact]
    public void ViolationSeverity_HasTwoValues()
    {
        Enum.GetValues<ViolationSeverity>().Should().HaveCount(2);
    }
}

public class ModelDefaultTests
{
    [Fact]
    public void Person_HasEmptyDefaults()
    {
        var person = new Person();
        person.Name.Should().BeEmpty();
        person.SpeaksDutch.Should().BeFalse();
        person.SpeaksPolish.Should().BeFalse();
        person.DefaultDaysOff.Should().BeEmpty();
        person.Skills.Should().BeEmpty();
        person.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TaskDefinition_HasCorrectDefaults()
    {
        var task = new TaskDefinition();
        task.Name.Should().BeEmpty();
        task.IsActive.Should().BeTrue();
        task.HeadcountRequired.Should().Be(1);
        task.BoardPosition.Should().Be(BoardPosition.Left);
        task.RestLevel.Should().Be(SkillLevel.Beginner);
        task.RequiresLanguageCollaboration.Should().BeFalse();
    }

    [Fact]
    public void PlanningModel_HasEmptyDefaults()
    {
        var planning = new PlanningModel();
        planning.Assignments.Should().BeEmpty();
        planning.CustomTasks.Should().BeEmpty();
        planning.PinnedAssignments.Should().BeEmpty();
        planning.AbsentWorkers.Should().BeEmpty();
        planning.Warnings.Should().BeEmpty();
        planning.IsTemplate.Should().BeFalse();
    }
}
