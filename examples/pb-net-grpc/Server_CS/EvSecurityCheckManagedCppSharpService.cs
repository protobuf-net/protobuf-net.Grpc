using CVGrpcCppSharpLib.EvSecurityCheckManagedCppSharpContract;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using System;
using System.Threading.Tasks;

namespace CVGrpcCppSharpService;

public interface ICVDotNetLogger { }
public interface INativeObjectsMemoryAddressManager { }

[CodeFirstBinder]
partial class EvSecurityCheckManagedCppSharpService
{
    sealed class CodeFirstBinderAttribute : CodeFirstBinderBaseAttribute<EvSecurityCheckManagedCppSharpService>
    {
        public override int Bind(ILogger logger, ServiceMethodProviderContext<EvSecurityCheckManagedCppSharpService> context, BinderConfiguration binderConfiguration)
        {
            context.AddUnaryMethod<DisposeObjRequest, DisposeObjResponse>(
    new Method<DisposeObjRequest, DisposeObjResponse>(MethodType.Unary,
    nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.DisposeObj),
    BinderConfiguration.Default.GetMarshaller<DisposeObjRequest>(),
    BinderConfiguration.Default.GetMarshaller<DisposeObjResponse>()), [],
    (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).DisposeObj(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<getResponseStringRequest, getResponseStringResponse>(
                new Method<getResponseStringRequest, getResponseStringResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.getResponseString),
                BinderConfiguration.Default.GetMarshaller<getResponseStringRequest>(),
                BinderConfiguration.Default.GetMarshaller<getResponseStringResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).getResponseString(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<getBrowseErrorStringAndGuiMessageIdRequest, getBrowseErrorStringAndGuiMessageIdResponse>(
                new Method<getBrowseErrorStringAndGuiMessageIdRequest, getBrowseErrorStringAndGuiMessageIdResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.getBrowseErrorStringAndGuiMessageId),
                BinderConfiguration.Default.GetMarshaller<getBrowseErrorStringAndGuiMessageIdRequest>(),
                BinderConfiguration.Default.GetMarshaller<getBrowseErrorStringAndGuiMessageIdResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).getBrowseErrorStringAndGuiMessageId(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<getLastCapabilityIdRequest, getLastCapabilityIdResponse>(
                new Method<getLastCapabilityIdRequest, getLastCapabilityIdResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.getLastCapabilityId),
                BinderConfiguration.Default.GetMarshaller<getLastCapabilityIdRequest>(),
                BinderConfiguration.Default.GetMarshaller<getLastCapabilityIdResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).getLastCapabilityId(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canAdministerAdminScheduleRequest, canAdministerAdminScheduleResponse>(
                new Method<canAdministerAdminScheduleRequest, canAdministerAdminScheduleResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canAdministerAdminSchedule),
                BinderConfiguration.Default.GetMarshaller<canAdministerAdminScheduleRequest>(),
                BinderConfiguration.Default.GetMarshaller<canAdministerAdminScheduleResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canAdministerAdminSchedule(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canAdministerUserRequest, canAdministerUserResponse>(
                new Method<canAdministerUserRequest, canAdministerUserResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canAdministerUser),
                BinderConfiguration.Default.GetMarshaller<canAdministerUserRequest>(),
                BinderConfiguration.Default.GetMarshaller<canAdministerUserResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canAdministerUser(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canAdminLibraryRequest, canAdminLibraryResponse>(
                new Method<canAdminLibraryRequest, canAdminLibraryResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canAdminLibrary),
                BinderConfiguration.Default.GetMarshaller<canAdminLibraryRequest>(),
                BinderConfiguration.Default.GetMarshaller<canAdminLibraryResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canAdminLibrary(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canAdminLibraryMARequest, canAdminLibraryMAResponse>(
                new Method<canAdminLibraryMARequest, canAdminLibraryMAResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canAdminLibraryMA),
                BinderConfiguration.Default.GetMarshaller<canAdminLibraryMARequest>(),
                BinderConfiguration.Default.GetMarshaller<canAdminLibraryMAResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canAdminLibraryMA(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canBackupRequest, canBackupResponse>(
                new Method<canBackupRequest, canBackupResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canBackup),
                BinderConfiguration.Default.GetMarshaller<canBackupRequest>(),
                BinderConfiguration.Default.GetMarshaller<canBackupResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canBackup(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canBrowseRequest, canBrowseResponse>(
                new Method<canBrowseRequest, canBrowseResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canBrowse),
                BinderConfiguration.Default.GetMarshaller<canBrowseRequest>(),
                BinderConfiguration.Default.GetMarshaller<canBrowseResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canBrowse(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canDeleteLMScheduleRequest, canDeleteLMScheduleResponse>(
                new Method<canDeleteLMScheduleRequest, canDeleteLMScheduleResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canDeleteLMSchedule),
                BinderConfiguration.Default.GetMarshaller<canDeleteLMScheduleRequest>(),
                BinderConfiguration.Default.GetMarshaller<canDeleteLMScheduleResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canDeleteLMSchedule(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canEditLMScheduleRequest, canEditLMScheduleResponse>(
                new Method<canEditLMScheduleRequest, canEditLMScheduleResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canEditLMSchedule),
                BinderConfiguration.Default.GetMarshaller<canEditLMScheduleRequest>(),
                BinderConfiguration.Default.GetMarshaller<canEditLMScheduleResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canEditLMSchedule(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canEditShareRequest, canEditShareResponse>(
                new Method<canEditShareRequest, canEditShareResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canEditShare),
                BinderConfiguration.Default.GetMarshaller<canEditShareRequest>(),
                BinderConfiguration.Default.GetMarshaller<canEditShareResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canEditShare(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canExecuteWorkflowRequest, canExecuteWorkflowResponse>(
                new Method<canExecuteWorkflowRequest, canExecuteWorkflowResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canExecuteWorkflow),
                BinderConfiguration.Default.GetMarshaller<canExecuteWorkflowRequest>(),
                BinderConfiguration.Default.GetMarshaller<canExecuteWorkflowResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canExecuteWorkflow(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageApplicationRequest, canManageApplicationResponse>(
                new Method<canManageApplicationRequest, canManageApplicationResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageApplication),
                BinderConfiguration.Default.GetMarshaller<canManageApplicationRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageApplicationResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageApplication(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageArchiveRequest, canManageArchiveResponse>(
                new Method<canManageArchiveRequest, canManageArchiveResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageArchive),
                BinderConfiguration.Default.GetMarshaller<canManageArchiveRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageArchiveResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageArchive(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageCommServeRequest, canManageCommServeResponse>(
                new Method<canManageCommServeRequest, canManageCommServeResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageCommServe),
                BinderConfiguration.Default.GetMarshaller<canManageCommServeRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageCommServeResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageCommServe(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageDownloadCenterRequest, canManageDownloadCenterResponse>(
                new Method<canManageDownloadCenterRequest, canManageDownloadCenterResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageDownloadCenter),
                BinderConfiguration.Default.GetMarshaller<canManageDownloadCenterRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageDownloadCenterResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageDownloadCenter(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageImportExportRequest, canManageImportExportResponse>(
                new Method<canManageImportExportRequest, canManageImportExportResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageImportExport),
                BinderConfiguration.Default.GetMarshaller<canManageImportExportRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageImportExportResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageImportExport(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageInstallationRequest, canManageInstallationResponse>(
                new Method<canManageInstallationRequest, canManageInstallationResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageInstallation),
                BinderConfiguration.Default.GetMarshaller<canManageInstallationRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageInstallationResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageInstallation(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageLicenseRequest, canManageLicenseResponse>(
                new Method<canManageLicenseRequest, canManageLicenseResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageLicense),
                BinderConfiguration.Default.GetMarshaller<canManageLicenseRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageLicenseResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageLicense(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageMAForLibRequest, canManageMAForLibResponse>(
                new Method<canManageMAForLibRequest, canManageMAForLibResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageMAForLib),
                BinderConfiguration.Default.GetMarshaller<canManageMAForLibRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageMAForLibResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageMAForLib(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageOperationRequest, canManageOperationResponse>(
                new Method<canManageOperationRequest, canManageOperationResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageOperation),
                BinderConfiguration.Default.GetMarshaller<canManageOperationRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageOperationResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageOperation(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageOperationByMediaAgentRequest, canManageOperationByMediaAgentResponse>(
                new Method<canManageOperationByMediaAgentRequest, canManageOperationByMediaAgentResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageOperationByMediaAgent),
                BinderConfiguration.Default.GetMarshaller<canManageOperationByMediaAgentRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageOperationByMediaAgentResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageOperationByMediaAgent(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageReportsRequest, canManageReportsResponse>(
                new Method<canManageReportsRequest, canManageReportsResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageReports),
                BinderConfiguration.Default.GetMarshaller<canManageReportsRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageReportsResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageReports(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageUserRequest, canManageUserResponse>(
                new Method<canManageUserRequest, canManageUserResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageUser),
                BinderConfiguration.Default.GetMarshaller<canManageUserRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageUserResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageUser(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageVaultTrackerRequest, canManageVaultTrackerResponse>(
                new Method<canManageVaultTrackerRequest, canManageVaultTrackerResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageVaultTracker),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageVaultTracker(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageVaultTrackerContainerRequest, canManageVaultTrackerContainerResponse>(
                new Method<canManageVaultTrackerContainerRequest, canManageVaultTrackerContainerResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerContainer),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerContainerRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerContainerResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageVaultTrackerContainer(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageVaultTrackerLibraryRequest, canManageVaultTrackerLibraryResponse>(
                new Method<canManageVaultTrackerLibraryRequest, canManageVaultTrackerLibraryResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerLibrary),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerLibraryRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerLibraryResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageVaultTrackerLibrary(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageVaultTrackerPolicyRequest, canManageVaultTrackerPolicyResponse>(
                new Method<canManageVaultTrackerPolicyRequest, canManageVaultTrackerPolicyResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerPolicy),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerPolicyRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerPolicyResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageVaultTrackerPolicy(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageVaultTrackerShelfRequest, canManageVaultTrackerShelfResponse>(
                new Method<canManageVaultTrackerShelfRequest, canManageVaultTrackerShelfResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerShelf),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerShelfRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageVaultTrackerShelfResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageVaultTrackerShelf(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageWorkflowRequest, canManageWorkflowResponse>(
                new Method<canManageWorkflowRequest, canManageWorkflowResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageWorkflow),
                BinderConfiguration.Default.GetMarshaller<canManageWorkflowRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageWorkflowResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageWorkflow(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canOverwriteDataRequest, canOverwriteDataResponse>(
                new Method<canOverwriteDataRequest, canOverwriteDataResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canOverwriteData),
                BinderConfiguration.Default.GetMarshaller<canOverwriteDataRequest>(),
                BinderConfiguration.Default.GetMarshaller<canOverwriteDataResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canOverwriteData(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformDownloadRequest, canPerformDownloadResponse>(
                new Method<canPerformDownloadRequest, canPerformDownloadResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformDownload),
                BinderConfiguration.Default.GetMarshaller<canPerformDownloadRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformDownloadResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformDownload(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformFullRecoveryRequest, canPerformFullRecoveryResponse>(
                new Method<canPerformFullRecoveryRequest, canPerformFullRecoveryResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformFullRecovery),
                BinderConfiguration.Default.GetMarshaller<canPerformFullRecoveryRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformFullRecoveryResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformFullRecovery(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformLicenseUploadRequest, canPerformLicenseUploadResponse>(
                new Method<canPerformLicenseUploadRequest, canPerformLicenseUploadResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformLicenseUpload),
                BinderConfiguration.Default.GetMarshaller<canPerformLicenseUploadRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformLicenseUploadResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformLicenseUpload(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformLiveBrowseRequest, canPerformLiveBrowseResponse>(
                new Method<canPerformLiveBrowseRequest, canPerformLiveBrowseResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformLiveBrowse),
                BinderConfiguration.Default.GetMarshaller<canPerformLiveBrowseRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformLiveBrowseResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformLiveBrowse(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformLMOperationRequest, canPerformLMOperationResponse>(
                new Method<canPerformLMOperationRequest, canPerformLMOperationResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformLMOperation),
                BinderConfiguration.Default.GetMarshaller<canPerformLMOperationRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformLMOperationResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformLMOperation(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformOnDemandLMUploadRequest, canPerformOnDemandLMUploadResponse>(
                new Method<canPerformOnDemandLMUploadRequest, canPerformOnDemandLMUploadResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformOnDemandLMUpload),
                BinderConfiguration.Default.GetMarshaller<canPerformOnDemandLMUploadRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformOnDemandLMUploadResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformOnDemandLMUpload(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<EvSecurityCheck_canPerformShareRequest, EvSecurityCheck_canPerformShareResponse>(
                new Method<EvSecurityCheck_canPerformShareRequest, EvSecurityCheck_canPerformShareResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.EvSecurityCheck_canPerformShare),
                BinderConfiguration.Default.GetMarshaller<EvSecurityCheck_canPerformShareRequest>(),
                BinderConfiguration.Default.GetMarshaller<EvSecurityCheck_canPerformShareResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).EvSecurityCheck_canPerformShare(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformUploadRequest, canPerformUploadResponse>(
                new Method<canPerformUploadRequest, canPerformUploadResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformUpload),
                BinderConfiguration.Default.GetMarshaller<canPerformUploadRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformUploadResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformUpload(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canPerformWorkFlowFileUploadRequest, canPerformWorkFlowFileUploadResponse>(
                new Method<canPerformWorkFlowFileUploadRequest, canPerformWorkFlowFileUploadResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canPerformWorkFlowFileUpload),
                BinderConfiguration.Default.GetMarshaller<canPerformWorkFlowFileUploadRequest>(),
                BinderConfiguration.Default.GetMarshaller<canPerformWorkFlowFileUploadResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canPerformWorkFlowFileUpload(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canRestoreRequest, canRestoreResponse>(
                new Method<canRestoreRequest, canRestoreResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canRestore),
                BinderConfiguration.Default.GetMarshaller<canRestoreRequest>(),
                BinderConfiguration.Default.GetMarshaller<canRestoreResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canRestore(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canRestore2Request, canRestore2Response>(
                new Method<canRestore2Request, canRestore2Response>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canRestore2),
                BinderConfiguration.Default.GetMarshaller<canRestore2Request>(),
                BinderConfiguration.Default.GetMarshaller<canRestore2Response>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canRestore2(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canRestore3Request, canRestore3Response>(
                new Method<canRestore3Request, canRestore3Response>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canRestore3),
                BinderConfiguration.Default.GetMarshaller<canRestore3Request>(),
                BinderConfiguration.Default.GetMarshaller<canRestore3Response>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canRestore3(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canRestore4Request, canRestore4Response>(
                new Method<canRestore4Request, canRestore4Response>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canRestore4),
                BinderConfiguration.Default.GetMarshaller<canRestore4Request>(),
                BinderConfiguration.Default.GetMarshaller<canRestore4Response>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canRestore4(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canRunVMeRequest, canRunVMeResponse>(
                new Method<canRunVMeRequest, canRunVMeResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canRunVMe),
                BinderConfiguration.Default.GetMarshaller<canRunVMeRequest>(),
                BinderConfiguration.Default.GetMarshaller<canRunVMeResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canRunVMe(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canScheduleBackupRequest, canScheduleBackupResponse>(
                new Method<canScheduleBackupRequest, canScheduleBackupResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canScheduleBackup),
                BinderConfiguration.Default.GetMarshaller<canScheduleBackupRequest>(),
                BinderConfiguration.Default.GetMarshaller<canScheduleBackupResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canScheduleBackup(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canScheduleRestoreRequest, canScheduleRestoreResponse>(
                new Method<canScheduleRestoreRequest, canScheduleRestoreResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canScheduleRestore),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestoreRequest>(),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestoreResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canScheduleRestore(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canScheduleRestore2Request, canScheduleRestore2Response>(
                new Method<canScheduleRestore2Request, canScheduleRestore2Response>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canScheduleRestore2),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestore2Request>(),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestore2Response>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canScheduleRestore2(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canScheduleRestore3Request, canScheduleRestore3Response>(
                new Method<canScheduleRestore3Request, canScheduleRestore3Response>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canScheduleRestore3),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestore3Request>(),
                BinderConfiguration.Default.GetMarshaller<canScheduleRestore3Response>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canScheduleRestore3(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canViewShareRequest, canViewShareResponse>(
                new Method<canViewShareRequest, canViewShareResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canViewShare),
                BinderConfiguration.Default.GetMarshaller<canViewShareRequest>(),
                BinderConfiguration.Default.GetMarshaller<canViewShareResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canViewShare(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canViewShareIncludingExcludedRequest, canViewShareIncludingExcludedResponse>(
                new Method<canViewShareIncludingExcludedRequest, canViewShareIncludingExcludedResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canViewShareIncludingExcluded),
                BinderConfiguration.Default.GetMarshaller<canViewShareIncludingExcludedRequest>(),
                BinderConfiguration.Default.GetMarshaller<canViewShareIncludingExcludedResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canViewShareIncludingExcluded(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<getADUserInfoFromEmailRequest, getADUserInfoFromEmailResponse>(
                new Method<getADUserInfoFromEmailRequest, getADUserInfoFromEmailResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.getADUserInfoFromEmail),
                BinderConfiguration.Default.GetMarshaller<getADUserInfoFromEmailRequest>(),
                BinderConfiguration.Default.GetMarshaller<getADUserInfoFromEmailResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).getADUserInfoFromEmail(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<userHasCapabilityRequest, userHasCapabilityResponse>(
                new Method<userHasCapabilityRequest, userHasCapabilityResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.userHasCapability),
                BinderConfiguration.Default.GetMarshaller<userHasCapabilityRequest>(),
                BinderConfiguration.Default.GetMarshaller<userHasCapabilityResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).userHasCapability(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<validateClientDataSecurityRequest, validateClientDataSecurityResponse>(
                new Method<validateClientDataSecurityRequest, validateClientDataSecurityResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.validateClientDataSecurity),
                BinderConfiguration.Default.GetMarshaller<validateClientDataSecurityRequest>(),
                BinderConfiguration.Default.GetMarshaller<validateClientDataSecurityResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).validateClientDataSecurity(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canViewLockedClientDataRequest, canViewLockedClientDataResponse>(
                new Method<canViewLockedClientDataRequest, canViewLockedClientDataResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canViewLockedClientData),
                BinderConfiguration.Default.GetMarshaller<canViewLockedClientDataRequest>(),
                BinderConfiguration.Default.GetMarshaller<canViewLockedClientDataResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canViewLockedClientData(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<DisposeRequest, DisposeResponse>(
                new Method<DisposeRequest, DisposeResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.Dispose),
                BinderConfiguration.Default.GetMarshaller<DisposeRequest>(),
                BinderConfiguration.Default.GetMarshaller<DisposeResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).Dispose(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<canManageUsersRequest, canManageUsersResponse>(
                new Method<canManageUsersRequest, canManageUsersResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.canManageUsers),
                BinderConfiguration.Default.GetMarshaller<canManageUsersRequest>(),
                BinderConfiguration.Default.GetMarshaller<canManageUsersResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).canManageUsers(req, new(svc, ctx)).AsTask());
            context.AddUnaryMethod<usercanSaveAsScriptAndBrowseRequest, usercanSaveAsScriptAndBrowseResponse>(
                new Method<usercanSaveAsScriptAndBrowseRequest, usercanSaveAsScriptAndBrowseResponse>(MethodType.Unary,
                nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.usercanSaveAsScriptAndBrowse),
                BinderConfiguration.Default.GetMarshaller<usercanSaveAsScriptAndBrowseRequest>(),
                BinderConfiguration.Default.GetMarshaller<usercanSaveAsScriptAndBrowseResponse>()), [],
                (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).usercanSaveAsScriptAndBrowse(req, new(svc, ctx)).AsTask());


            //foreach (var method in typeof(IEvSecurityCheckManagedCppSharpService).GetMethods())
            //{
            //    var req = method.GetParameters()[0].ParameterType.Name;
            //    var resp = method.ReturnType.GenericTypeArguments[0].Name;
            //    Console.WriteLine($"""
            //        context.AddUnaryMethod<{req}, {resp}>(
            //            new Method<{req}, {resp}>(MethodType.Unary,
            //            nameof(EvSecurityNewManagedCppSharpService), nameof(IEvSecurityCheckManagedCppSharpService.{method.Name}),
            //            BinderConfiguration.Default.GetMarshaller<{req}>(),
            //            BinderConfiguration.Default.GetMarshaller<{resp}>()), [],
            //            (svc, req, ctx) => ((IEvSecurityCheckManagedCppSharpService)svc).{method.Name}(req, new(svc, ctx)).AsTask());
            //        """);
            //}
            return 999;
        }
    }
}

public partial class EvSecurityCheckManagedCppSharpService : IEvSecurityCheckManagedCppSharpService
{
    private readonly ICVDotNetLogger logger;
    private readonly INativeObjectsMemoryAddressManager _nativeObjectsMemoryAddressManager;
    public EvSecurityCheckManagedCppSharpService(ICVDotNetLogger logger, INativeObjectsMemoryAddressManager nativeObjectsMemoryAddressManager)
    {
        this.logger = logger;
        this._nativeObjectsMemoryAddressManager = nativeObjectsMemoryAddressManager;
    }

    ValueTask<canAdministerAdminScheduleResponse> IEvSecurityCheckManagedCppSharpService.canAdministerAdminSchedule(canAdministerAdminScheduleRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canAdministerUserResponse> IEvSecurityCheckManagedCppSharpService.canAdministerUser(canAdministerUserRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canAdminLibraryResponse> IEvSecurityCheckManagedCppSharpService.canAdminLibrary(canAdminLibraryRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canAdminLibraryMAResponse> IEvSecurityCheckManagedCppSharpService.canAdminLibraryMA(canAdminLibraryMARequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canBackupResponse> IEvSecurityCheckManagedCppSharpService.canBackup(canBackupRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canBrowseResponse> IEvSecurityCheckManagedCppSharpService.canBrowse(canBrowseRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canDeleteLMScheduleResponse> IEvSecurityCheckManagedCppSharpService.canDeleteLMSchedule(canDeleteLMScheduleRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canEditLMScheduleResponse> IEvSecurityCheckManagedCppSharpService.canEditLMSchedule(canEditLMScheduleRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canEditShareResponse> IEvSecurityCheckManagedCppSharpService.canEditShare(canEditShareRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canExecuteWorkflowResponse> IEvSecurityCheckManagedCppSharpService.canExecuteWorkflow(canExecuteWorkflowRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageApplicationResponse> IEvSecurityCheckManagedCppSharpService.canManageApplication(canManageApplicationRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageArchiveResponse> IEvSecurityCheckManagedCppSharpService.canManageArchive(canManageArchiveRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageCommServeResponse> IEvSecurityCheckManagedCppSharpService.canManageCommServe(canManageCommServeRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageDownloadCenterResponse> IEvSecurityCheckManagedCppSharpService.canManageDownloadCenter(canManageDownloadCenterRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageImportExportResponse> IEvSecurityCheckManagedCppSharpService.canManageImportExport(canManageImportExportRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageInstallationResponse> IEvSecurityCheckManagedCppSharpService.canManageInstallation(canManageInstallationRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageLicenseResponse> IEvSecurityCheckManagedCppSharpService.canManageLicense(canManageLicenseRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageMAForLibResponse> IEvSecurityCheckManagedCppSharpService.canManageMAForLib(canManageMAForLibRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageOperationResponse> IEvSecurityCheckManagedCppSharpService.canManageOperation(canManageOperationRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageOperationByMediaAgentResponse> IEvSecurityCheckManagedCppSharpService.canManageOperationByMediaAgent(canManageOperationByMediaAgentRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageReportsResponse> IEvSecurityCheckManagedCppSharpService.canManageReports(canManageReportsRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageUserResponse> IEvSecurityCheckManagedCppSharpService.canManageUser(canManageUserRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageUsersResponse> IEvSecurityCheckManagedCppSharpService.canManageUsers(canManageUsersRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageVaultTrackerResponse> IEvSecurityCheckManagedCppSharpService.canManageVaultTracker(canManageVaultTrackerRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageVaultTrackerContainerResponse> IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerContainer(canManageVaultTrackerContainerRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageVaultTrackerLibraryResponse> IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerLibrary(canManageVaultTrackerLibraryRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageVaultTrackerPolicyResponse> IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerPolicy(canManageVaultTrackerPolicyRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageVaultTrackerShelfResponse> IEvSecurityCheckManagedCppSharpService.canManageVaultTrackerShelf(canManageVaultTrackerShelfRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canManageWorkflowResponse> IEvSecurityCheckManagedCppSharpService.canManageWorkflow(canManageWorkflowRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canOverwriteDataResponse> IEvSecurityCheckManagedCppSharpService.canOverwriteData(canOverwriteDataRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformDownloadResponse> IEvSecurityCheckManagedCppSharpService.canPerformDownload(canPerformDownloadRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformFullRecoveryResponse> IEvSecurityCheckManagedCppSharpService.canPerformFullRecovery(canPerformFullRecoveryRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformLicenseUploadResponse> IEvSecurityCheckManagedCppSharpService.canPerformLicenseUpload(canPerformLicenseUploadRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformLiveBrowseResponse> IEvSecurityCheckManagedCppSharpService.canPerformLiveBrowse(canPerformLiveBrowseRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformLMOperationResponse> IEvSecurityCheckManagedCppSharpService.canPerformLMOperation(canPerformLMOperationRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformOnDemandLMUploadResponse> IEvSecurityCheckManagedCppSharpService.canPerformOnDemandLMUpload(canPerformOnDemandLMUploadRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformUploadResponse> IEvSecurityCheckManagedCppSharpService.canPerformUpload(canPerformUploadRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canPerformWorkFlowFileUploadResponse> IEvSecurityCheckManagedCppSharpService.canPerformWorkFlowFileUpload(canPerformWorkFlowFileUploadRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canRestoreResponse> IEvSecurityCheckManagedCppSharpService.canRestore(canRestoreRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canRestore2Response> IEvSecurityCheckManagedCppSharpService.canRestore2(canRestore2Request request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canRestore3Response> IEvSecurityCheckManagedCppSharpService.canRestore3(canRestore3Request request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canRestore4Response> IEvSecurityCheckManagedCppSharpService.canRestore4(canRestore4Request request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canRunVMeResponse> IEvSecurityCheckManagedCppSharpService.canRunVMe(canRunVMeRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canScheduleBackupResponse> IEvSecurityCheckManagedCppSharpService.canScheduleBackup(canScheduleBackupRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canScheduleRestoreResponse> IEvSecurityCheckManagedCppSharpService.canScheduleRestore(canScheduleRestoreRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canScheduleRestore2Response> IEvSecurityCheckManagedCppSharpService.canScheduleRestore2(canScheduleRestore2Request request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canScheduleRestore3Response> IEvSecurityCheckManagedCppSharpService.canScheduleRestore3(canScheduleRestore3Request request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canViewLockedClientDataResponse> IEvSecurityCheckManagedCppSharpService.canViewLockedClientData(canViewLockedClientDataRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canViewShareResponse> IEvSecurityCheckManagedCppSharpService.canViewShare(canViewShareRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<canViewShareIncludingExcludedResponse> IEvSecurityCheckManagedCppSharpService.canViewShareIncludingExcluded(canViewShareIncludingExcludedRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<DisposeResponse> IEvSecurityCheckManagedCppSharpService.Dispose(DisposeRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<DisposeObjResponse> IEvSecurityCheckManagedCppSharpService.DisposeObj(DisposeObjRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<EvSecurityCheck_canPerformShareResponse> IEvSecurityCheckManagedCppSharpService.EvSecurityCheck_canPerformShare(EvSecurityCheck_canPerformShareRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<getADUserInfoFromEmailResponse> IEvSecurityCheckManagedCppSharpService.getADUserInfoFromEmail(getADUserInfoFromEmailRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<getBrowseErrorStringAndGuiMessageIdResponse> IEvSecurityCheckManagedCppSharpService.getBrowseErrorStringAndGuiMessageId(getBrowseErrorStringAndGuiMessageIdRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<getLastCapabilityIdResponse> IEvSecurityCheckManagedCppSharpService.getLastCapabilityId(getLastCapabilityIdRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<getResponseStringResponse> IEvSecurityCheckManagedCppSharpService.getResponseString(getResponseStringRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<usercanSaveAsScriptAndBrowseResponse> IEvSecurityCheckManagedCppSharpService.usercanSaveAsScriptAndBrowse(usercanSaveAsScriptAndBrowseRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<userHasCapabilityResponse> IEvSecurityCheckManagedCppSharpService.userHasCapability(userHasCapabilityRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }

    ValueTask<validateClientDataSecurityResponse> IEvSecurityCheckManagedCppSharpService.validateClientDataSecurity(validateClientDataSecurityRequest request, CallContext context)
    {
        throw new NotImplementedException();
    }
}