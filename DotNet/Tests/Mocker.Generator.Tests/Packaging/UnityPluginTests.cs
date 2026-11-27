using NUnit.Framework;
using reromanlee.Mocker.Generator.Mocker;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace reromanlee.Mocker.Generator.Tests.Packaging
{
    internal sealed class UnityPluginTests
    {
        private const string FingerprintKey = "SourceFingerprint";
        private const string PluginFileName = "reromanlee.Mocker.Generator.dll";
        private const string RepositoryPrefix = "/Mocker/";
        private const string UpdateCommand = "dotnet build DotNet/Mocker.slnx --configuration Release";
        private static readonly Guid SourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");

        [Test]
        public void UnityPackageHoldsCurrentGeneratorLibrary()
        {
            string pluginsFolder = Path.Combine(FindRepositoryRoot(), "UnityPackage", "Runtime", "Plugins");
            string pluginPath = Path.Combine(pluginsFolder, PluginFileName);

            Assert.IsTrue(File.Exists(pluginPath), $"Missing generator plugin DLL. Build with: {UpdateCommand}");

            string expected = typeof(MockerGenerator).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == FingerprintKey).Value;
            string packaged = ReadAssemblyAttribute(pluginPath, nameof(AssemblyMetadataAttribute), FingerprintKey);

            Assert.AreEqual(expected, packaged, $"Stale generator plugin DLL in UnityPackage. Rebuild with: {UpdateCommand}");
        }

        [Test]
        public void UnityPackageGeneratorLibraryContainsCleanDeterministicPdb()
        {
            string pluginPath = Path.Combine(FindRepositoryRoot(), "UnityPackage", "Runtime", "Plugins", PluginFileName);
            string version = ReadAssemblyAttribute(pluginPath, nameof(AssemblyInformationalVersionAttribute), null);

            Assert.IsNotNull(version);
            Assert.IsFalse(version.Contains('+'), $"Informational version leaks commit hash: {version}");

            using (FileStream stream = File.OpenRead(pluginPath))
            using (PEReader peReader = new PEReader(stream))
            {
                DebugDirectoryEntry embedded = peReader.ReadDebugDirectory()
                    .Single(entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);

                using (MetadataReaderProvider provider = peReader.ReadEmbeddedPortablePdbDebugDirectoryData(embedded))
                {
                    MetadataReader pdb = provider.GetMetadataReader();

                    foreach (CustomDebugInformationHandle handle in pdb.CustomDebugInformation)
                    {
                        Assert.AreNotEqual(SourceLinkKind, pdb.GetGuid(pdb.GetCustomDebugInformation(handle).Kind));
                    }

                    foreach (DocumentHandle handle in pdb.Documents)
                    {
                        string document = pdb.GetString(pdb.GetDocument(handle).Name);
                        StringAssert.StartsWith(RepositoryPrefix, document);
                    }
                }
            }
        }

        private static string ReadAssemblyAttribute(string assemblyPath, string attributeName, string key)
        {
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (PEReader peReader = new PEReader(stream))
            {
                MetadataReader reader = peReader.GetMetadataReader();

                foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
                {
                    CustomAttribute attribute = reader.GetCustomAttribute(handle);

                    if (attribute.Constructor.Kind != HandleKind.MemberReference)
                    {
                        continue;
                    }

                    MemberReference constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

                    if (constructor.Parent.Kind != HandleKind.TypeReference
                        || reader.GetString(reader.GetTypeReference((TypeReferenceHandle)constructor.Parent).Name) != attributeName)
                    {
                        continue;
                    }

                    BlobReader blob = reader.GetBlobReader(attribute.Value);
                    blob.ReadUInt16();
                    string first = blob.ReadSerializedString();

                    if (key == null)
                    {
                        return first;
                    }

                    if (first == key)
                    {
                        return blob.ReadSerializedString();
                    }
                }
            }

            return null;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "UnityPackage")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Tests ran outside the repository root.");
            }

            return directory.FullName;
        }
    }
}
