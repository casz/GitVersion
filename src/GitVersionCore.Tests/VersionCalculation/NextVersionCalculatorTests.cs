using System;
using System.Collections.Generic;
using GitTools.Testing;
using GitVersion.Configuration;
using GitVersion.VersionCalculation;
using GitVersion.VersioningModes;
using GitVersionCore.Tests.Mocks;
using LibGit2Sharp;
using NUnit.Framework;
using Shouldly;
using GitVersion;
using GitVersion.Extensions;
using GitVersionCore.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace GitVersionCore.Tests.VersionCalculation
{
    public class NextVersionCalculatorTests : TestBase
    {
        [Test]
        public void ShouldIncrementVersionBasedOnConfig()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 1, "master", "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now);

            var sp = ConfigureServices(services =>
            {
                services.AddSingleton<IBaseVersionCalculator>(new TestBaseVersionCalculator(true, new SemanticVersion(1), new MockCommit()));
                services.AddSingleton<IMetaDataCalculator>(new TestMetaDataCalculator(semanticVersionBuildMetaData));
            });

            var nextVersionCalculator = sp.GetService<INextVersionCalculator>();

            var context = new GitVersionContextBuilder().WithConfig(new Config()).Build();

            var version = nextVersionCalculator.FindVersion(context);

            version.ToString().ShouldBe("1.0.1");
        }

        [Test]
        public void DoesNotIncrementWhenBaseVersionSaysNotTo()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 1, "master", "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now);
            var sp = ConfigureServices(services =>
            {
                services.AddSingleton<IBaseVersionCalculator>(new TestBaseVersionCalculator(false, new SemanticVersion(1), new MockCommit()));
                services.AddSingleton<IMetaDataCalculator>(new TestMetaDataCalculator(semanticVersionBuildMetaData));
            });

            var nextVersionCalculator = sp.GetService<INextVersionCalculator>();

            var context = new GitVersionContextBuilder().WithConfig(new Config()).Build();

            var version = nextVersionCalculator.FindVersion(context);

            version.ToString().ShouldBe("1.0.0");
        }

        [Test]
        public void AppliesBranchPreReleaseTag()
        {
            var semanticVersionBuildMetaData = new SemanticVersionBuildMetaData("ef7d0d7e1e700f1c7c9fa01ea6791bb778a5c37c", 2, "develop", "b1a34edbd80e141f7cc046c074f109be7d022074", "b1a34e", DateTimeOffset.Now);

            var sp = ConfigureServices(services =>
            {
                services.AddSingleton<IBaseVersionCalculator>(new TestBaseVersionCalculator(false, new SemanticVersion(1), new MockCommit()));
                services.AddSingleton<IMetaDataCalculator>(new TestMetaDataCalculator(semanticVersionBuildMetaData));
            });

            var nextVersionCalculator = sp.GetService<INextVersionCalculator>();

            var context = new GitVersionContextBuilder()
                .WithDevelopBranch()
                .Build();

            var version = nextVersionCalculator.FindVersion(context);

            version.ToString("f").ShouldBe("1.0.0-alpha.1+2");
        }

        [Test]
        public void PreReleaseTagCanUseBranchName()
        {
            var config = new Config
            {
                NextVersion = "1.0.0",
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        "custom", new BranchConfig
                        {
                            Regex = "custom/",
                            Tag = "useBranchName",
                            SourceBranches = new List<string>()
                        }
                    }
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("develop");
            fixture.MakeACommit();
            fixture.BranchTo("custom/foo");
            fixture.MakeACommit();

            fixture.AssertFullSemver(config, "1.0.0-foo.1+2");
        }

        [Test]
        public void PreReleaseTagCanUseBranchNameVariable()
        {
            var config = new Config
            {
                NextVersion = "1.0.0",
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        "custom", new BranchConfig
                        {
                            Regex = "custom/",
                            Tag = "alpha.{BranchName}",
                            SourceBranches = new List<string>()
                        }
                    }
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.MakeACommit();
            fixture.BranchTo("develop");
            fixture.MakeACommit();
            fixture.BranchTo("custom/foo");
            fixture.MakeACommit();

            fixture.AssertFullSemver(config, "1.0.0-alpha.foo.1+2");
        }

        [Test]
        public void PreReleaseNumberShouldBeScopeToPreReleaseLabelInContinuousDelivery()
        {
            var config = new Config
            {
                VersioningMode = VersioningMode.ContinuousDelivery,
                Branches = new Dictionary<string, BranchConfig>
                {
                    {
                        "master", new BranchConfig()
                        {
                            Tag = "beta"
                        }
                    },
                }
            };

            using var fixture = new EmptyRepositoryFixture();
            fixture.Repository.MakeACommit();

            fixture.Repository.CreateBranch("feature/test");
            Commands.Checkout(fixture.Repository, "feature/test");
            fixture.Repository.MakeATaggedCommit("0.1.0-test.1");
            fixture.Repository.MakeACommit();

            fixture.AssertFullSemver(config, "0.1.0-test.2+2");

            Commands.Checkout(fixture.Repository, "master");
            fixture.Repository.Merge(fixture.Repository.FindBranch("feature/test"), Generate.SignatureNow());

            fixture.AssertFullSemver(config, "0.1.0-beta.1+2");
        }
    }
}
