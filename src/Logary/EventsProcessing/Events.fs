namespace Logary.EventsProcessing

open Logary
open Logary.Internals
open Hopac
open Hopac.Infixes
open NodaTime

[<RequireQualifiedAccessAttribute>]
module Events =
  type Processing = Pipe<Message,Alt<Promise<unit>>,Message> // for logary target based processing

  type B = B


  type T =
    private {
      pipes : Processing list
    }

  let stream = {
    pipes = List.empty;
  }

  let events<'r> = Pipe.start<Message,'r>

  let subscribers pipes stream =
    {pipes = List.concat [pipes; stream.pipes;]}

  let service svc pipe =
    pipe |> Pipe.filter (fun msg -> Message.tryGetContext KnownLiterals.ServiceContextName msg = Some svc)

  let tag tag pipe = pipe |> Pipe.filter (Message.hasTag tag)

  let miniLevel level pipe =
    pipe |> Pipe.filter (fun msg -> msg.level >= level)


  let sink (names : string list) pipe =
    pipe |> Pipe.map (Message.addSinks names)

  let flattenToProcessing (pipe: Pipe<seq<Message>,Alt<Promise<unit>>,Message>) =
    pipe
    |> Pipe.chain (fun logWithAck ->
       fun (msgs: seq<_>) ->
          let alllogedAcks = IVar ()

          let logAllConJob =
            msgs
            |> Hopac.Extensions.Seq.Con.mapJob (fun msg -> 
               logWithAck msg |> PipeResult.orDefault (Promise.instaPromise))

          let logAllAlt = Alt.prepareJob <| fun _ ->
            Job.start (logAllConJob >>= fun acks -> IVar.fill alllogedAcks acks)
            >>-. alllogedAcks

          logAllAlt ^-> fun acks -> Job.conIgnore acks |> memo
          |> PipeResult.HasResult)

  let toProcessing stream =
    let pipes = stream.pipes
    let allTickTimerJobs = List.collect (fun pipe -> pipe.tickTimerJobs) pipes
    // let latch = Latch pipes.Length

    let build =
      fun cont ->
        let allBuildedSource = pipes |> List.map (fun pipe -> pipe.build cont)
        fun sourceItem ->
          let composed =
            allBuildedSource
            |> List.traverseAltA (fun onNext -> onNext sourceItem |> PipeResult.orDefault (Promise.instaPromise))
          composed
          ^-> (Hopac.Job.conIgnore >> memo)
          |> PipeResult.HasResult

    { build = build
      tickTimerJobs = allTickTimerJobs
    }