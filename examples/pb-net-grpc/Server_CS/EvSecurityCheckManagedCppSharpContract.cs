using ProtoBuf;
using ProtoBuf.Grpc;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
namespace CVGrpcCppSharpLib.EvSecurityCheckManagedCppSharpContract;

[ProtoContract(SkipConstructor = true)]
public class DisposeObjRequest
{
    
    [ProtoMember(1)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(2)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class DisposeObjResponse
{
    
    [ProtoMember(1)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(2)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(3)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(4)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getResponseStringRequest
{
    
    [ProtoMember(1)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(2)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getResponseStringResponse
{
    
    [ProtoMember(1)]
    public  String  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getBrowseErrorStringAndGuiMessageIdRequest
{
    
    [ProtoMember(1)]
    public  Int32  errorCode { get; set; }
    
    [ProtoMember(2)]
    public  String  errorString { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  guiMsgId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getBrowseErrorStringAndGuiMessageIdResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  Int32  errorCode { get; set; }
    
    [ProtoMember(3)]
    public  String  errorString { get; set; }
    
    [ProtoMember(4)]
    public  UInt32  guiMsgId { get; set; }
    
    [ProtoMember(5)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(6)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(7)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(8)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getLastCapabilityIdRequest
{
    
    [ProtoMember(1)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(2)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getLastCapabilityIdResponse
{
    
    [ProtoMember(1)]
    public  Int32  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdministerAdminScheduleRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdministerAdminScheduleResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdministerUserRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdministerUserResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdminLibraryRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  libraryId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdminLibraryResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdminLibraryMARequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  libraryId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  mediaAgentId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  localeId { get; set; }
    
    [ProtoMember(6)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(7)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(8)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(9)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(10)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canAdminLibraryMAResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canBackupRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canBackupResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canBrowseRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  UInt32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  UInt32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isEndUser { get; set; }
    
    [ProtoMember(9)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(10)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(11)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(12)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(13)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canBrowseResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canDeleteLMScheduleRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  monitoringPolicyId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canDeleteLMScheduleResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditLMScheduleRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  monitoringPolicyId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditLMScheduleResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditShareRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  shareId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditShareResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canExecuteWorkflowRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  workflowId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canExecuteWorkflowResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageApplicationRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageApplicationResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageArchiveRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  archiveGroupId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageArchiveResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageCommServeRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageCommServeResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageDownloadCenterRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageDownloadCenterResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageImportExportRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  libraryId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageImportExportResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageInstallationRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageInstallationResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageLicenseRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageLicenseResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageMAForLibRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  libraryId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  mediaAgentId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  localeId { get; set; }
    
    [ProtoMember(6)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(7)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(8)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(9)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(10)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageMAForLibResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageOperationRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageOperationResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageOperationByMediaAgentRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  mediaAgentId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageOperationByMediaAgentResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageReportsRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageReportsResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageUserRequest
{
    
    [ProtoMember(1)]
    public  Int32  managerId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  managedId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageUserResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerContainerRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  id { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerContainerResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerLibraryRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  id { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerLibraryResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerPolicyRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  id { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerPolicyResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerShelfRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  id { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageVaultTrackerShelfResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageWorkflowRequest
{
    
    [ProtoMember(1)]
    public  IntPtr  obj { get; set; }
    
    [ProtoMember(2)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  workflowId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageWorkflowResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canOverwriteDataRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  String  passkeyW { get; set; }
    
    [ProtoMember(6)]
    public  Int32  flag { get; set; }
    
    [ProtoMember(7)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(8)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(9)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(10)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(11)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canOverwriteDataResponse
{
    
    [ProtoMember(1)]
    public  Int32  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformDownloadRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformDownloadResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformFullRecoveryRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformFullRecoveryResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLicenseUploadRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLicenseUploadResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLiveBrowseRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLiveBrowseResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLMOperationRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  monitoringPolicyId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  opType { get; set; }
    
    [ProtoMember(5)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(6)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(7)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(8)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(9)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformLMOperationResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformOnDemandLMUploadRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  templateId { get; set; }
    
    [ProtoMember(4)]
    public  String  stagingArea { get; set; }
    
    [ProtoMember(5)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  LMPolicyId { get; set; }
    
    [ProtoMember(7)]
    public  String  errorString { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformOnDemandLMUploadResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  String  stagingArea { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  LMPolicyId { get; set; }
    
    [ProtoMember(5)]
    public  String  errorString { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(8)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(9)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityCheck_canPerformShareRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityCheck_canPerformShareResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformUploadRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformUploadResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformWorkFlowFileUploadRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  workFlowId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  jobId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  o_isValidClient { get; set; }
    
    [ProtoMember(6)]
    public  Int32  o_isValidJob { get; set; }
    
    [ProtoMember(7)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(8)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(9)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(10)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(11)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canPerformWorkFlowFileUploadResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  Int32  o_isValidClient { get; set; }
    
    [ProtoMember(3)]
    public  Int32  o_isValidJob { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(6)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(7)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestoreRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(9)]
    public  Boolean  outOfPlace { get; set; }
    
    [ProtoMember(10)]
    public  Boolean  endUser { get; set; }
    
    [ProtoMember(11)]
    public  Boolean  impersonationProvided { get; set; }
    
    [ProtoMember(12)]
    public  String  machineUId { get; set; }
    
    [ProtoMember(13)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(14)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(15)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(16)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(17)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestoreResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore2Request
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(9)]
    public  Boolean  outOfPlace { get; set; }
    
    [ProtoMember(10)]
    public  Boolean  endUser { get; set; }
    
    [ProtoMember(11)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(12)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(13)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(14)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(15)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore2Response
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore3Request
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(9)]
    public  Boolean  outOfPlace { get; set; }
    
    [ProtoMember(10)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(11)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(12)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(13)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(14)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore3Response
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore4Request
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(9)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(10)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(11)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(12)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(13)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRestore4Response
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRunVMeRequest
{
    
    [ProtoMember(1)]
    public  String  userName { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commcellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(8)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(9)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(10)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(11)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canRunVMeResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleBackupRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  subClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleBackupResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestoreRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  outOfPlace { get; set; }
    
    [ProtoMember(9)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(10)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(11)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(12)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(13)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestoreResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestore2Request
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  outOfPlace { get; set; }
    
    [ProtoMember(9)]
    public  Boolean  endUser { get; set; }
    
    [ProtoMember(10)]
    public  Boolean  impersonationProvided { get; set; }
    
    [ProtoMember(11)]
    public  String  machineUId { get; set; }
    
    [ProtoMember(12)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(13)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(14)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(15)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(16)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestore2Response
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestore3Request
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  srcClientId { get; set; }
    
    [ProtoMember(4)]
    public  Int32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  Int32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  Int32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  Int32  dstClientId { get; set; }
    
    [ProtoMember(8)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(11)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(12)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canScheduleRestore3Response
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewShareRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  shareId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewShareResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewShareIncludingExcludedRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  shareId { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewShareIncludingExcludedResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getADUserInfoFromEmailRequest
{
    
    [ProtoMember(1)]
    public  String  login { get; set; }
    
    [ProtoMember(2)]
    public  Int32  providerIdToLookUp { get; set; }
    
    [ProtoMember(3)]
    public  String  samlAppKey { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getADUserInfoFromEmailResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  String  login { get; set; }
    
    [ProtoMember(3)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(4)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(5)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(6)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class userHasCapabilityRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  capabilityId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class userHasCapabilityResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class validateClientDataSecurityRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  entityId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  entityType { get; set; }
    
    [ProtoMember(4)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(7)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class validateClientDataSecurityResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewLockedClientDataRequest
{
    
    [ProtoMember(1)]
    public  UInt32  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt32  commCellId { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  clientId { get; set; }
    
    [ProtoMember(4)]
    public  UInt32  appTypeId { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  instanceId { get; set; }
    
    [ProtoMember(6)]
    public  UInt32  backupSetId { get; set; }
    
    [ProtoMember(7)]
    public  UInt32  subclientId { get; set; }
    
    [ProtoMember(8)]
    public  Boolean  endUser { get; set; }
    
    [ProtoMember(9)]
    public  String  passkeyW { get; set; }
    
    [ProtoMember(10)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(11)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(12)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(13)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(14)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canViewLockedClientDataResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class DisposeRequest
{
    
    [ProtoMember(1)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(2)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(3)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class DisposeResponse
{
    
    [ProtoMember(1)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(2)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(3)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(4)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageUsersRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(5)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(6)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canManageUsersResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class usercanSaveAsScriptAndBrowseRequest
{
    
    [ProtoMember(1)]
    public  UInt64  userId { get; set; }
    
    [ProtoMember(2)]
    public  UInt64  clientId { get; set; }
    
    [ProtoMember(3)]
    public  int  constructorNumber { get; set; }
    
    [ProtoMember(4)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(5)]
    public  UInt32  constructorParam2 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class usercanSaveAsScriptAndBrowseResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityCheckExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ServiceContract]   
public interface IEvSecurityCheckManagedCppSharpService
{
    [OperationContract]
    public ValueTask<DisposeObjResponse> DisposeObj (DisposeObjRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<getResponseStringResponse> getResponseString (getResponseStringRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<getBrowseErrorStringAndGuiMessageIdResponse> getBrowseErrorStringAndGuiMessageId (getBrowseErrorStringAndGuiMessageIdRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<getLastCapabilityIdResponse> getLastCapabilityId (getLastCapabilityIdRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canAdministerAdminScheduleResponse> canAdministerAdminSchedule (canAdministerAdminScheduleRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canAdministerUserResponse> canAdministerUser (canAdministerUserRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canAdminLibraryResponse> canAdminLibrary (canAdminLibraryRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canAdminLibraryMAResponse> canAdminLibraryMA (canAdminLibraryMARequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canBackupResponse> canBackup (canBackupRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canBrowseResponse> canBrowse (canBrowseRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canDeleteLMScheduleResponse> canDeleteLMSchedule (canDeleteLMScheduleRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canEditLMScheduleResponse> canEditLMSchedule (canEditLMScheduleRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canEditShareResponse> canEditShare (canEditShareRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canExecuteWorkflowResponse> canExecuteWorkflow (canExecuteWorkflowRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageApplicationResponse> canManageApplication (canManageApplicationRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageArchiveResponse> canManageArchive (canManageArchiveRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageCommServeResponse> canManageCommServe (canManageCommServeRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageDownloadCenterResponse> canManageDownloadCenter (canManageDownloadCenterRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageImportExportResponse> canManageImportExport (canManageImportExportRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageInstallationResponse> canManageInstallation (canManageInstallationRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageLicenseResponse> canManageLicense (canManageLicenseRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageMAForLibResponse> canManageMAForLib (canManageMAForLibRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageOperationResponse> canManageOperation (canManageOperationRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageOperationByMediaAgentResponse> canManageOperationByMediaAgent (canManageOperationByMediaAgentRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageReportsResponse> canManageReports (canManageReportsRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageUserResponse> canManageUser (canManageUserRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageVaultTrackerResponse> canManageVaultTracker (canManageVaultTrackerRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageVaultTrackerContainerResponse> canManageVaultTrackerContainer (canManageVaultTrackerContainerRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageVaultTrackerLibraryResponse> canManageVaultTrackerLibrary (canManageVaultTrackerLibraryRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageVaultTrackerPolicyResponse> canManageVaultTrackerPolicy (canManageVaultTrackerPolicyRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageVaultTrackerShelfResponse> canManageVaultTrackerShelf (canManageVaultTrackerShelfRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageWorkflowResponse> canManageWorkflow (canManageWorkflowRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canOverwriteDataResponse> canOverwriteData (canOverwriteDataRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformDownloadResponse> canPerformDownload (canPerformDownloadRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformFullRecoveryResponse> canPerformFullRecovery (canPerformFullRecoveryRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformLicenseUploadResponse> canPerformLicenseUpload (canPerformLicenseUploadRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformLiveBrowseResponse> canPerformLiveBrowse (canPerformLiveBrowseRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformLMOperationResponse> canPerformLMOperation (canPerformLMOperationRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformOnDemandLMUploadResponse> canPerformOnDemandLMUpload (canPerformOnDemandLMUploadRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<EvSecurityCheck_canPerformShareResponse> EvSecurityCheck_canPerformShare (EvSecurityCheck_canPerformShareRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformUploadResponse> canPerformUpload (canPerformUploadRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canPerformWorkFlowFileUploadResponse> canPerformWorkFlowFileUpload (canPerformWorkFlowFileUploadRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canRestoreResponse> canRestore (canRestoreRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canRestore2Response> canRestore2 (canRestore2Request request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canRestore3Response> canRestore3 (canRestore3Request request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canRestore4Response> canRestore4 (canRestore4Request request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canRunVMeResponse> canRunVMe (canRunVMeRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canScheduleBackupResponse> canScheduleBackup (canScheduleBackupRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canScheduleRestoreResponse> canScheduleRestore (canScheduleRestoreRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canScheduleRestore2Response> canScheduleRestore2 (canScheduleRestore2Request request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canScheduleRestore3Response> canScheduleRestore3 (canScheduleRestore3Request request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canViewShareResponse> canViewShare (canViewShareRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canViewShareIncludingExcludedResponse> canViewShareIncludingExcluded (canViewShareIncludingExcludedRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<getADUserInfoFromEmailResponse> getADUserInfoFromEmail (getADUserInfoFromEmailRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<userHasCapabilityResponse> userHasCapability (userHasCapabilityRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<validateClientDataSecurityResponse> validateClientDataSecurity (validateClientDataSecurityRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canViewLockedClientDataResponse> canViewLockedClientData (canViewLockedClientDataRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<DisposeResponse> Dispose (DisposeRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canManageUsersResponse> canManageUsers (canManageUsersRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<usercanSaveAsScriptAndBrowseResponse> usercanSaveAsScriptAndBrowse (usercanSaveAsScriptAndBrowseRequest request, CallContext context = default);
    
}