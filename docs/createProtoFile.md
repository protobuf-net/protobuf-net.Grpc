# protobuf-net.Reflection

## What is it?

> ProtoBuf DSL (proto2 / proto3) and descriptor tools for protobuf-net

It use to create .proto file for your service

## Install?

Install [`protobuf-net.Reflection`](https://www.nuget.org/packages/protobuf-net.Reflection) on your project

## How use it?

After install change your service and add these lines: 

``` c#
var generator = new SchemaGenerator
{
    ProtoSyntax = ProtoSyntax.Proto3
};

var schema = generator.GetSchema<ICalculator>(); // there is also a non-generic overload that takes Type

using (var writer = new System.IO.StreamWriter("services.proto"))
{
    await writer.WriteAsync(schema);
}
```

Now build your project. Your .proto file is exist on your bin/Debug or bin/Realase

Output example:  

```c#
syntax = "proto3";
package Hyper;

message MultiplyRequest {
   int32 X = 1;
   int32 Y = 2;
}
message MultiplyResult {
   int32 Result = 1;
}
service Calculator {
   rpc Multiply (MultiplyRequest) returns (MultiplyResult);
}
```
