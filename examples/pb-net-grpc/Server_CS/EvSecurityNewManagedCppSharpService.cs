using CVGrpcCppSharpLib.EvSecurityNewManagedCppSharpContract;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Grpc.Server;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace CVGrpcCppSharpService;

[CodeFirstBinder]
partial class EvSecurityNewManagedCppSharpService
{
    sealed class CodeFirstBinderAttribute : CodeFirstBinderBaseAttribute<EvSecurityNewManagedCppSharpService>
    {
        public override int Bind(ILogger logger, ServiceMethodProviderContext<EvSecurityNewManagedCppSharpService> context, BinderConfiguration binderConfiguration)
        {
            context.AddUnaryMethod<DisposeObjRequest, DisposeObjResponse>(
    new Method<DisposeObjRequest, DisposeObjResponse>(MethodType.Unary,
    nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.DisposeObj),
    BinderConfiguration.Default.GetMarshaller<DisposeObjRequest>(),
    BinderConfiguration.Default.GetMarshaller<DisposeObjResponse>()), [],
    (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).DisposeObj(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<EvSecurityNew_getErrorCodeRequest, EvSecurityNew_getErrorCodeResponse>(
                new Method<EvSecurityNew_getErrorCodeRequest, EvSecurityNew_getErrorCodeResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.EvSecurityNew_getErrorCode),
                BinderConfiguration.Default.GetMarshaller<EvSecurityNew_getErrorCodeRequest>(),
                BinderConfiguration.Default.GetMarshaller<EvSecurityNew_getErrorCodeResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).EvSecurityNew_getErrorCode(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<hasPermissionWithoutAssociationRequest, hasPermissionWithoutAssociationResponse>(
                new Method<hasPermissionWithoutAssociationRequest, hasPermissionWithoutAssociationResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.hasPermissionWithoutAssociation),
                BinderConfiguration.Default.GetMarshaller<hasPermissionWithoutAssociationRequest>(),
                BinderConfiguration.Default.GetMarshaller<hasPermissionWithoutAssociationResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).hasPermissionWithoutAssociation(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canCreateSchedulePolicyRequest, canCreateSchedulePolicyResponse>(
                new Method<canCreateSchedulePolicyRequest, canCreateSchedulePolicyResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.canCreateSchedulePolicy),
                BinderConfiguration.Default.GetMarshaller<canCreateSchedulePolicyRequest>(),
                BinderConfiguration.Default.GetMarshaller<canCreateSchedulePolicyResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).canCreateSchedulePolicy(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canDeleteSchedulePolicyRequest, canDeleteSchedulePolicyResponse>(
                new Method<canDeleteSchedulePolicyRequest, canDeleteSchedulePolicyResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.canDeleteSchedulePolicy),
                BinderConfiguration.Default.GetMarshaller<canDeleteSchedulePolicyRequest>(),
                BinderConfiguration.Default.GetMarshaller<canDeleteSchedulePolicyResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).canDeleteSchedulePolicy(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canEditSchedulePolicyRequest, canEditSchedulePolicyResponse>(
                new Method<canEditSchedulePolicyRequest, canEditSchedulePolicyResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.canEditSchedulePolicy),
                BinderConfiguration.Default.GetMarshaller<canEditSchedulePolicyRequest>(),
                BinderConfiguration.Default.GetMarshaller<canEditSchedulePolicyResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).canEditSchedulePolicy(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canEditCustomReportRequest, canEditCustomReportResponse>(
                new Method<canEditCustomReportRequest, canEditCustomReportResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.canEditCustomReport),
                BinderConfiguration.Default.GetMarshaller<canEditCustomReportRequest>(),
                BinderConfiguration.Default.GetMarshaller<canEditCustomReportResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).canEditCustomReport(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canExecuteCustomReportRequest, canExecuteCustomReportResponse>(
                new Method<canExecuteCustomReportRequest, canExecuteCustomReportResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.canExecuteCustomReport),
                BinderConfiguration.Default.GetMarshaller<canExecuteCustomReportRequest>(),
                BinderConfiguration.Default.GetMarshaller<canExecuteCustomReportResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).canExecuteCustomReport(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<EvSecurityNew_canRecallRequest, EvSecurityNew_canRecallResponse>(
                new Method<EvSecurityNew_canRecallRequest, EvSecurityNew_canRecallResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.EvSecurityNew_canRecall),
                BinderConfiguration.Default.GetMarshaller<EvSecurityNew_canRecallRequest>(),
                BinderConfiguration.Default.GetMarshaller<EvSecurityNew_canRecallResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).EvSecurityNew_canRecall(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<hasPermissionForUserOnEntityRequest, hasPermissionForUserOnEntityResponse>(
                new Method<hasPermissionForUserOnEntityRequest, hasPermissionForUserOnEntityResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.hasPermissionForUserOnEntity),
                BinderConfiguration.Default.GetMarshaller<hasPermissionForUserOnEntityRequest>(),
                BinderConfiguration.Default.GetMarshaller<hasPermissionForUserOnEntityResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).hasPermissionForUserOnEntity(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<getResponseStringRequest, getResponseStringResponse>(
                new Method<getResponseStringRequest, getResponseStringResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.getResponseString),
                BinderConfiguration.Default.GetMarshaller<getResponseStringRequest>(),
                BinderConfiguration.Default.GetMarshaller<getResponseStringResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).getResponseString(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<DisposeRequest, DisposeResponse>(
                new Method<DisposeRequest, DisposeResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityNewManagedCppSharpService.Dispose),
                BinderConfiguration.Default.GetMarshaller<DisposeRequest>(),
                BinderConfiguration.Default.GetMarshaller<DisposeResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityNewManagedCppSharpService)svc).Dispose(req, new(svc, ctx)).AsTask());

            return 999;
        }
    }
}
public partial class EvSecurityNewManagedCppSharpService : IEvSecurityNewManagedCppSharpService
{


    private readonly ICVDotNetLogger logger;
    private readonly INativeObjectsMemoryAddressManager _nativeObjectsMemoryAddressManager;
    public EvSecurityNewManagedCppSharpService(ICVDotNetLogger logger, INativeObjectsMemoryAddressManager nativeObjectsMemoryAddressManager)
    {
        this.logger = logger;
        this._nativeObjectsMemoryAddressManager = nativeObjectsMemoryAddressManager;
    }

    ValueTask<canCreateSchedulePolicyResponse> IEvSecurityNewManagedCppSharpService.canCreateSchedulePolicy(canCreateSchedulePolicyRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canDeleteSchedulePolicyResponse> IEvSecurityNewManagedCppSharpService.canDeleteSchedulePolicy(canDeleteSchedulePolicyRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canEditCustomReportResponse> IEvSecurityNewManagedCppSharpService.canEditCustomReport(canEditCustomReportRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canEditSchedulePolicyResponse> IEvSecurityNewManagedCppSharpService.canEditSchedulePolicy(canEditSchedulePolicyRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canExecuteCustomReportResponse> IEvSecurityNewManagedCppSharpService.canExecuteCustomReport(canExecuteCustomReportRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<DisposeResponse> IEvSecurityNewManagedCppSharpService.Dispose(DisposeRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<DisposeObjResponse> IEvSecurityNewManagedCppSharpService.DisposeObj(DisposeObjRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<EvSecurityNew_canRecallResponse> IEvSecurityNewManagedCppSharpService.EvSecurityNew_canRecall(EvSecurityNew_canRecallRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<EvSecurityNew_getErrorCodeResponse> IEvSecurityNewManagedCppSharpService.EvSecurityNew_getErrorCode(EvSecurityNew_getErrorCodeRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<getResponseStringResponse> IEvSecurityNewManagedCppSharpService.getResponseString(getResponseStringRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<hasPermissionForUserOnEntityResponse> IEvSecurityNewManagedCppSharpService.hasPermissionForUserOnEntity(hasPermissionForUserOnEntityRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<hasPermissionWithoutAssociationResponse> IEvSecurityNewManagedCppSharpService.hasPermissionWithoutAssociation(hasPermissionWithoutAssociationRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }
}