using Amazon.Lambda.Core;
using System;
using System.Diagnostics;
using System.Threading;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace LambdaRemoteDebug.Example
{
    public class Handler
    {
        public Response Hello(Request request)
        {
            Console.WriteLine("Process: " + Process.GetCurrentProcess().Id);

            LambdaRemoteDebug.Attach();

            int i;

            for (i = 1; i <= 10; i++)
            {
                Console.WriteLine(i);
                Thread.Sleep(500);
            }

            return new Response("Done: " + i, request);
        }
    }

    public class Response
    {
        public string Message { get; set; }
        public Request Request { get; set; }

        public Response(string message, Request request)
        {
            Message = message;
            Request = request;
        }
    }

    public class Request
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Key3 { get; set; }

        public Request(string key1, string key2, string key3)
        {
            Key1 = key1;
            Key2 = key2;
            Key3 = key3;
        }
    }
}
