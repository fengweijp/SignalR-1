// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;

namespace Microsoft.AspNetCore.Sockets
{
    public class HttpConnection : IPipelineConnection
    {
        public HttpConnection(PipelineFactory factory)
        {
            Input = factory.Create();
            Output = factory.Create();
        }

        IPipelineReader IPipelineConnection.Input => Input;

        IPipelineWriter IPipelineConnection.Output => Output;

        public PipelineReaderWriter Input { get; }

        public PipelineReaderWriter Output { get; }

        public void Dispose()
        {
            Input.CompleteReader();
            Input.CompleteWriter();

            Output.CompleteReader();
            Output.CompleteWriter();
        }
    }
}
