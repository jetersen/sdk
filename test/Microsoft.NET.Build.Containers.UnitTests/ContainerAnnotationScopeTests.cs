// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ContainerAnnotationScopeTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("Manifest", true, false)]
    [InlineData(" index ", false, true)]
    [InlineData(" INDEX, manifest ", true, true)]
    public void FiltersScopes(string? scope, bool appliesToManifest, bool appliesToIndex)
    {
        TaskItem annotation = new("example.annotation");
        if (scope is not null)
        {
            annotation.SetMetadata("Scope", scope);
        }
        var task = new TestTask { BuildEngine = new Mock<IBuildEngine>().Object };

        Assert.True(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Manifest, task.Log, out ITaskItem[] manifests));
        Assert.Equal(appliesToManifest ? 1 : 0, manifests.Length);
        Assert.True(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Index, task.Log, out ITaskItem[] indexes));
        Assert.Equal(appliesToIndex ? 1 : 0, indexes.Length);
    }

    [Fact]
    public void RejectsInvalidScope()
    {
        TaskItem annotation = new("example.annotation");
        annotation.SetMetadata("Scope", "Manifest,Descriptor");
        var task = new TestTask { BuildEngine = new Mock<IBuildEngine>().Object };

        Assert.False(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Manifest, task.Log, out _));
        Assert.True(task.Log.HasLoggedErrors);
    }

    [Fact]
    public void ResolvesEmptyCreatedAnnotationFromBuildTimestamp()
    {
        DateTime createdAt = DateTimeOffset.FromUnixTimeSeconds(1636374896).UtcDateTime;
        TaskItem annotation = new(ContainerAnnotationScopes.CreatedAnnotationName);

        Assert.Equal("2021-11-08T12:34:56.0000000Z", ContainerAnnotationScopes.GetValue(annotation, createdAt));
    }

    [Fact]
    public void PreservesExplicitCreatedAnnotationValue()
    {
        TaskItem annotation = new(ContainerAnnotationScopes.CreatedAnnotationName);
        annotation.SetMetadata("Value", "explicit");

        Assert.Equal("explicit", ContainerAnnotationScopes.GetValue(annotation, DateTime.UnixEpoch));
    }

    private sealed class TestTask : Microsoft.Build.Utilities.Task
    {
        public override bool Execute() => true;
    }
}
