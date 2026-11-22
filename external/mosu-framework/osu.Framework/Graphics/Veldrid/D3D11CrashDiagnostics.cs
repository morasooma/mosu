using System;
using osu.Framework.Configuration;
using SharpGen.Runtime;

namespace osu.Framework.Graphics.Veldrid
{
    internal static class D3D11CrashDiagnostics
    {
        // Also read by the client's crash report exporter. Only this diagnostic entry is exported, not arbitrary exception data.
        public const string DataKey = "Mosu.D3D11.DeviceRemovedReason";

        public static string? Annotate(Exception exception, RendererType renderer, string adapter, Func<int> getDeviceRemovedReason)
        {
            if (renderer != RendererType.Direct3D11 && renderer != RendererType.Deferred_Direct3D11)
                return null;

            var failure = findDeviceFailure(exception);
            if (failure == null)
                return null;
            if (failure.Data[DataKey] is string existing)
                return existing;

            string reason;
            try
            {
                reason = FormatResult(getDeviceRemovedReason());
            }
            catch (Exception probeException)
            {
                // Diagnostic failure must not replace the original rendering exception.
                reason = $"unavailable ({probeException.GetType().Name}: {probeException.Message})";
            }

            string diagnostic = $"Direct3D 11 device diagnostics: renderer={renderer}; GPU={adapter}; "
                                   + $"original={FormatResult(failure.ResultCode.Code)}; GetDeviceRemovedReason={reason}. "
                                   + "This is diagnostic information, not proof of a hardware fault or driver bug.";
            failure.Data[DataKey] = diagnostic;
            return diagnostic;
        }

        private static SharpGenException? findDeviceFailure(Exception exception)
        {
            if (exception is SharpGenException sharpGen && isDeviceFailure(sharpGen.ResultCode.Code))
                return sharpGen;

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    var failure = findDeviceFailure(inner);
                    if (failure != null)
                        return failure;
                }

                return null;
            }

            return exception.InnerException == null ? null : findDeviceFailure(exception.InnerException);
        }

        private static bool isDeviceFailure(int code)
            => code == unchecked((int)0x887A0005) || code == unchecked((int)0x887A0006)
               || code == unchecked((int)0x887A0007) || code == unchecked((int)0x887A0020);

        public static string FormatResult(int code)
        {
            string name = code switch
            {
                0 => "S_OK",
                unchecked((int)0x887A0001) => "DXGI_ERROR_INVALID_CALL",
                unchecked((int)0x887A0005) => "DXGI_ERROR_DEVICE_REMOVED",
                unchecked((int)0x887A0006) => "DXGI_ERROR_DEVICE_HUNG",
                unchecked((int)0x887A0007) => "DXGI_ERROR_DEVICE_RESET",
                unchecked((int)0x887A0020) => "DXGI_ERROR_DRIVER_INTERNAL_ERROR",
                _ => "UNKNOWN",
            };
            return $"0x{code:X8} ({name})";
        }
    }
}
