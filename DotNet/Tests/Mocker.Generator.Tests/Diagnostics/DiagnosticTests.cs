using Microsoft.CodeAnalysis;
using NUnit.Framework;
using reromanlee.Mocker.Generator.Tests.Common;
using System.Linq;

namespace reromanlee.Mocker.Generator.Tests.Diagnostics
{
    internal sealed class DiagnosticTests
    {
        [Test]
        public void ReportsMock001WhenCompositeOrNodeOrComponentIsNotPartial()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract class Services { }

    [Node(""Player"", typeof(Services))]
    public class Player { }

    [Component(""Saves"", typeof(Player))]
    public interface ISaves { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock001Assembly", source);

            Assert.AreEqual(3, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK001"));
        }

        [Test]
        public void ReportsMock002WhenParentIsNull()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Node(""Player"", null)]
    public partial class Player { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock002Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK002"));
        }

        [Test]
        public void ReportsMock003WhenParentCarriesNoMockerAttribute()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    public class PlainParent { }

    [Node(""Player"", typeof(PlainParent))]
    public partial class Player { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock003Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK003"));
        }

        [Test]
        public void ReportsMock004WhenParentHasWrongRole()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISaves { }

    [Node(""Child"", typeof(ISaves))]
    public partial class ChildNode { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock004Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK004"));
        }

        [Test]
        public void ReportsMock005OnCircularParentChain()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Node(""Alpha"", typeof(Beta))]
    public partial class Alpha { }

    [Node(""Beta"", typeof(Alpha))]
    public partial class Beta { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock005Assembly", source);

            Assert.AreEqual(2, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK005"));
        }

        [Test]
        public void ReportsMock006OnDuplicateSiblingNames()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISavesA { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISavesB { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock006Assembly", source);

            Assert.AreEqual(2, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK006"));
        }

        [Test]
        public void ReportsMock007WhenImplementorDoesNotImplementInterfaceOrInheritAbstractComponent()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISaves { }

    [Component(""Audio"", typeof(Services))]
    public abstract partial class AudioService { }

    [Implementor(typeof(ISaves))]
    public class UnrelatedSaves { }

    [Implementor(typeof(AudioService))]
    public class UnrelatedAudio { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock007Assembly", source);

            Assert.AreEqual(2, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK007"));
        }

        [Test]
        public void ReportsMock008WhenImplementorIsNamedNone()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISaves { }

    [Implementor(typeof(ISaves))]
    public class None : ISaves { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock008Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK008"));
        }

        [Test]
        public void ReportsMock009WhenRootClaimsUnknownComposite()
        {
            string source = @"
using reromanlee.Mocker;

[assembly: MockerRoot(typeof(Demo.NotAComposite))]

namespace Demo
{
    public class NotAComposite { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock009Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK009"));
        }

        [Test]
        public void ReportsMock010WhenCompositeIsNotAbstract()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public partial class Services { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock010Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK010"));
        }

        [Test]
        public void ReportsMock011OnCustomReferenceAwaitableOrRefReturn()
        {
            string source = @"
using reromanlee.Mocker;
using System.Runtime.CompilerServices;

namespace Demo
{
    public sealed class CustomAwaiter : INotifyCompletion
    {
        public bool IsCompleted { get { return true; } }
        public void OnCompleted(System.Action continuation) { }
        public void GetResult() { }
    }

    public sealed class CustomAwaitable
    {
        public CustomAwaiter GetAwaiter() { return new CustomAwaiter(); }
    }

    [Composite]
    public abstract partial class Services { }

    [Component(""Net"", typeof(Services))]
    public partial interface INet
    {
        CustomAwaitable ConnectAsync();
    }

    [Component(""Buffer"", typeof(Services))]
    public partial interface IBuffer
    {
        ref int Current { get; }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock011Assembly", source);

            Assert.AreEqual(2, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK011"));
        }

        [Test]
        public void ReportsMock012WhenImplementorDependsOnForeignCompositeComponent()
        {
            string source = @"
using reromanlee.Mocker;

[assembly: MockerRoot]

namespace Demo
{
    [Composite]
    public abstract partial class FirstHub { }

    [Composite]
    public abstract partial class SecondHub { }

    [Component(""Clock"", typeof(FirstHub))]
    public partial interface IClock { }

    [Component(""Saves"", typeof(SecondHub))]
    public partial interface ISaves { }

    [Implementor(typeof(ISaves))]
    public class ForeignSaves : ISaves
    {
        public ForeignSaves(IClock clock) { }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock012Assembly", source);

            Assert.IsTrue(result.GeneratorDiagnostics.Any(d => d.Id == "MOCK012"));
        }

        [Test]
        public void ReportsMock013OnUnconstructibleOrAmbiguousImplementors()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Saves"", typeof(Services))]
    public partial interface ISaves { }

    [Implementor(typeof(ISaves))]
    public abstract class AbstractSaves : ISaves { }

    [Implementor(typeof(ISaves))]
    public class PrivateCtorSaves : ISaves
    {
        private PrivateCtorSaves() { }
    }

    [Implementor(typeof(ISaves))]
    public class AmbiguousSaves : ISaves
    {
        public AmbiguousSaves(string a) { }
        public AmbiguousSaves(int b) { }
    }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock013Assembly", source);

            Assert.AreEqual(3, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK013"));
        }

        [Test]
        public void ReportsMock014WhenComponentClassIsNotAbstract()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Audio"", typeof(Services))]
    public partial class ConcreteAudio { }

    [Component(""Video"", typeof(Services))]
    public static partial class StaticVideo { }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock014Assembly", source);

            Assert.AreEqual(2, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK014"));
        }

        [Test]
        public void ReportsMock015WhenAbstractComponentLacksParameterlessConstructorForMock()
        {
            string source = @"
using reromanlee.Mocker;

namespace Demo
{
    [Composite]
    public abstract partial class Services { }

    [Component(""Audio"", typeof(Services))]
    public abstract partial class AudioService
    {
        protected AudioService(string deviceName) { }
        public abstract void Play(string clip);
    }
}
";
            GenerationResult result = GeneratorHarness.Run("Mock015Assembly", source);

            Assert.AreEqual(1, result.GeneratorDiagnostics.Count(d => d.Id == "MOCK015"));
        }
    }
}
