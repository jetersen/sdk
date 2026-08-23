// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class ImageIndexGeneratorTests
{
    [Fact]
    public void ImagesCannotBeEmpty()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.ImagesEmpty, ex.Message);
    }

    [Fact]
    public void ImagesCannotBeEmpty_SpecifiedMediaType()
    {
        BuiltImage[] images = Array.Empty<BuiltImage>();
        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images, "manifestMediaType", "imageIndexMediaType"));
        Assert.Equal(Strings.ImagesEmpty, ex.Message);
    }

    [Fact]
    public void UnsupportedMediaTypeThrows()
    {
        BuiltImage[] images = 
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = "unsupported"
            }
        ];

        var ex = Assert.Throws<NotSupportedException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(string.Format(Strings.UnsupportedMediaType, "unsupported"), ex.Message);
    }

    [Theory]
    [InlineData(SchemaTypes.DockerManifestV2)]
    [InlineData(SchemaTypes.OciManifestV1)]
    public void ImagesWithMixedMediaTypes(string supportedMediaType)
    {
        BuiltImage[] images = 
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = supportedMediaType,
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "",
                ManifestDigest = "",
                ManifestMediaType = "anotherMediaType"
            }
        ];

        var ex = Assert.Throws<ArgumentException>(() => ImageIndexGenerator.GenerateImageIndex(images));
        Assert.Equal(Strings.MixedMediaTypes, ex.Message);
    }

    [Fact]
    public void GenerateDockerManifestList()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.DockerManifestV2,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = SchemaTypes.DockerManifestV2,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.docker.distribution.manifest.list.v2+json\",\"manifests\":[{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.docker.distribution.manifest.v2+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.Equal(SchemaTypes.DockerManifestListV2, mediaType);
    }

    [Fact]
    public void GenerateOciImageIndex()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.OciManifestV1,
                Architecture = "arch1",
                OS = "os1"
            },
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest2",
                ManifestMediaType = SchemaTypes.OciManifestV1,
                Architecture = "arch2",
                OS = "os2"
            }
        ];

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest1\",\"platform\":{\"architecture\":\"arch1\",\"os\":\"os1\"}},{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"size\":3,\"digest\":\"sha256:digest2\",\"platform\":{\"architecture\":\"arch2\",\"os\":\"os2\"}}]}", imageIndex);
        Assert.Equal(SchemaTypes.OciImageIndexV1, mediaType);
    }

    [Fact]
    public void GenerateOciImageIndexWithAnnotations()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.OciManifestV1,
                Architecture = "arch1",
                OS = "os1"
            }
        ];
        Dictionary<string, string> annotations = new()
        {
            ["org.opencontainers.image.source"] = "https://github.com/dotnet/sdk",
            ["org.opencontainers.image.revision"] = "abcdef"
        };
        Dictionary<string, string> annotationsInReverseOrder = new()
        {
            ["org.opencontainers.image.revision"] = "abcdef",
            ["org.opencontainers.image.source"] = "https://github.com/dotnet/sdk"
        };

        var (imageIndex, mediaType) = ImageIndexGenerator.GenerateImageIndex(images, annotations);
        var (imageIndexFromReverseOrder, _) = ImageIndexGenerator.GenerateImageIndex(images, annotationsInReverseOrder);

        Assert.Equal(imageIndex, imageIndexFromReverseOrder);
        Assert.Equal(SchemaTypes.OciImageIndexV1, mediaType);

        using JsonDocument document = JsonDocument.Parse(imageIndex);
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(SchemaTypes.OciImageIndexV1, root.GetProperty("mediaType").GetString());

        JsonElement manifest = root.GetProperty("manifests")[0];
        Assert.Equal(SchemaTypes.OciManifestV1, manifest.GetProperty("mediaType").GetString());
        Assert.Equal(3, manifest.GetProperty("size").GetInt64());
        Assert.Equal("sha256:digest1", manifest.GetProperty("digest").GetString());
        Assert.Equal("arch1", manifest.GetProperty("platform").GetProperty("architecture").GetString());
        Assert.Equal("os1", manifest.GetProperty("platform").GetProperty("os").GetString());

        JsonElement serializedAnnotations = root.GetProperty("annotations");
        Assert.Equal(2, serializedAnnotations.EnumerateObject().Count());
        Assert.Equal("https://github.com/dotnet/sdk", serializedAnnotations.GetProperty("org.opencontainers.image.source").GetString());
        Assert.Equal("abcdef", serializedAnnotations.GetProperty("org.opencontainers.image.revision").GetString());
    }

    [Fact]
    public void DockerManifestListDoesNotIncludeOciAnnotations()
    {
        BuiltImage[] images =
        [
            new BuiltImage
            {
                Config = "",
                Manifest = "123",
                ManifestDigest = "sha256:digest1",
                ManifestMediaType = SchemaTypes.DockerManifestV2,
                Architecture = "arch1",
                OS = "os1"
            }
        ];

        var (imageIndex, _) = ImageIndexGenerator.GenerateImageIndex(images, new Dictionary<string, string> { ["example.com/key"] = "value" });

        Assert.False(imageIndex.Contains("annotations", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateImageIndexWithAnnotations()
    {
        string imageIndex = ImageIndexGenerator.GenerateImageIndexWithAnnotations("mediaType", "sha256:digest", 3, "repository", ["1.0", "2.0"]);
        Assert.Equal("{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":[{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:1.0\",\"org.opencontainers.image.ref.name\":\"1.0\"}},{\"mediaType\":\"mediaType\",\"size\":3,\"digest\":\"sha256:digest\",\"platform\":{},\"annotations\":{\"io.containerd.image.name\":\"docker.io/library/repository:2.0\",\"org.opencontainers.image.ref.name\":\"2.0\"}}]}", imageIndex);
    }
}
