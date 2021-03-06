﻿namespace Logary.Configuration

open FluentMigrator

open System
open System.Data
open System.Runtime.CompilerServices

open Logary
open Logary.Configuration

open Logary.DB
open Logary.DB.Migrations
open Logary.Targets.DB

[<Extension>]
module MigrationBuilderEx =
  let private logger = Log.create "Logary.Configuration.Migrations"

  [<Extension; CompiledName "MigrateUp">]
  let migrateUp (builder: ThirdStep, processorFac: Func<IDbConnection, IMigrationProcessor>) =
    let builder' = builder :> ConfigReader<DBConf>
    let conf = builder'.ReadConf()
    let conn = conf.connectionFactory ()
    let fac = ExistingConnectionProcessorFactory(conn, processorFac.Invoke)
    Runner(fac, "", logger = logger).MigrateUp()
    builder.Done conf