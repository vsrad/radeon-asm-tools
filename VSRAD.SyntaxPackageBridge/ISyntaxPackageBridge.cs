namespace VSRAD.SyntaxPackageBridge
{
    /// <summary>
    /// Specifies the interface for inter-extension communication between VSRAD.Package and VSRAD.Syntax.
    /// The interface is implemented by VSRAD.Package, and VSRAD.Syntax requests the implementation via MEF at runtime.
    /// </summary>
    public interface ISyntaxPackageBridge
    {
    }
}
