using ProtoBuf;
using ProtoBuf.Grpc;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
namespace CVGrpcCppSharpLib.EvSecurityNewManagedCppSharpContract;

[ProtoContract(SkipConstructor = true)]
public class DisposeObjRequest
{
    
    [ProtoMember(1)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class DisposeObjResponse
{
    
    [ProtoMember(1)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(2)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(3)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(4)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityNew_getErrorCodeRequest
{
    
    [ProtoMember(1)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityNew_getErrorCodeResponse
{
    
    [ProtoMember(1)]
    public  Int32  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class hasPermissionWithoutAssociationRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  permissionId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class hasPermissionWithoutAssociationResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canCreateSchedulePolicyRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(3)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(4)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canCreateSchedulePolicyResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canDeleteSchedulePolicyRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  taskId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canDeleteSchedulePolicyResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditSchedulePolicyRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  taskId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditSchedulePolicyResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditCustomReportRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  customReportId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canEditCustomReportResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canExecuteCustomReportRequest
{
    
    [ProtoMember(1)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  customReportId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(4)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(5)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class canExecuteCustomReportResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityNew_canRecallRequest
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
    public  Int32  andOperation { get; set; }
    
    [ProtoMember(9)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(10)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(11)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class EvSecurityNew_canRecallResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class hasPermissionForUserOnEntityRequest
{
    
    [ProtoMember(1)]
    public  Int32  permissionId { get; set; }
    
    [ProtoMember(2)]
    public  Int32  userId { get; set; }
    
    [ProtoMember(3)]
    public  Int32  entityType1 { get; set; }
    
    [ProtoMember(4)]
    public  Int32  entityId1 { get; set; }
    
    [ProtoMember(5)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(6)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(7)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class hasPermissionForUserOnEntityResponse
{
    
    [ProtoMember(1)]
    public  Boolean  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(4)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(5)]
    public  String  responseErrorMessage { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getResponseStringRequest
{
    
    [ProtoMember(1)]
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class getResponseStringResponse
{
    
    [ProtoMember(1)]
    public  String  grpcRetVal { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
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
    public  Int32  constructorParam1 { get; set; }
    
    [ProtoMember(2)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(3)]
    public  Boolean  isDisposed { get; set; }
    
}
[ProtoContract(SkipConstructor = true)]
public class DisposeResponse
{
    
    [ProtoMember(1)]
    public  IntPtr  m_EvSecurityNewExtern { get; set; }
    
    [ProtoMember(2)]
    public  Boolean  isDisposed { get; set; }
    
    [ProtoMember(3)]
    public  Int16  responseErrorCode { get; set; }
    
    [ProtoMember(4)]
    public  String  responseErrorMessage { get; set; }
    
}
[ServiceContract]   
public interface IEvSecurityNewManagedCppSharpService
{
    
    [OperationContract]
    public ValueTask<DisposeObjResponse> DisposeObj (DisposeObjRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<EvSecurityNew_getErrorCodeResponse> EvSecurityNew_getErrorCode (EvSecurityNew_getErrorCodeRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<hasPermissionWithoutAssociationResponse> hasPermissionWithoutAssociation (hasPermissionWithoutAssociationRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canCreateSchedulePolicyResponse> canCreateSchedulePolicy (canCreateSchedulePolicyRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canDeleteSchedulePolicyResponse> canDeleteSchedulePolicy (canDeleteSchedulePolicyRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canEditSchedulePolicyResponse> canEditSchedulePolicy (canEditSchedulePolicyRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canEditCustomReportResponse> canEditCustomReport (canEditCustomReportRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<canExecuteCustomReportResponse> canExecuteCustomReport (canExecuteCustomReportRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<EvSecurityNew_canRecallResponse> EvSecurityNew_canRecall (EvSecurityNew_canRecallRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<hasPermissionForUserOnEntityResponse> hasPermissionForUserOnEntity (hasPermissionForUserOnEntityRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<getResponseStringResponse> getResponseString (getResponseStringRequest request, CallContext context = default);
    
    [OperationContract]
    public ValueTask<DisposeResponse> Dispose (DisposeRequest request, CallContext context = default);
    
}