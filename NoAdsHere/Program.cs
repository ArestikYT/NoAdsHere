﻿using System;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using NLog;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Discord.Commands;
using NoAdsHere.Services.AntiAds;
using NoAdsHere.Services.Configuration;
using NoAdsHere.Services.Penalties;
using NoAdsHere.Services.Violations;
using NoAdsHere.Database;
using NoAdsHere.Database.Models.GuildSettings;
using System.Linq;
using System.Collections.Generic;
using Discord.Addons.InteractiveCommands;
using NoAdsHere.Common;
using NoAdsHere.Services.FAQ;
using Quartz;
using Quartz.Impl;

namespace NoAdsHere
{
    internal class Program
    {
        private static void Main() =>
            new Program().RunAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private Config _config;
        private MongoClient _mongo;
        private readonly Logger _logger = LogManager.GetLogger("Core");
        private readonly Logger _discordLogger = LogManager.GetLogger("Discord");
        private bool _readyExecuted;
        private IServiceProvider _provider;
        private IScheduler _scheduler;

        public async Task RunAsync()
        {
            _logger.Info("Creating DiscordClient");
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
#if DEBUG
                LogLevel = LogSeverity.Debug,
#else
                LogLevel = LogSeverity.Verbose,
#endif
            });

            _client.Log += ClientLogger;
            _client.Ready += Ready;
            _client.JoinedGuild += JoinedGuild;

            _config = Config.Load();
            _mongo = CreateDatabaseConnection();
            _scheduler = await StartQuartz();
            DatabaseBase.Mongo = _mongo;
            DatabaseBase.Client = _client;

            _provider = ConfigureServices();

            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task Ready()
        {
            if (!_readyExecuted)
            {
                await Task.Delay(500);
                await CommandHandler.Install(_provider);
                await CommandHandler.ConfigureAsync();
                
                await Violations.Install(_provider);

                await AntiAds.Install(_provider);
                await AntiAds.StartAsync();

                await FaqService.Install(_provider);
                await FaqService.LoadFaqs();

                await JobQueue.Install(_provider);

                _readyExecuted = true;
            }
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            var logger = LogManager.GetLogger("AntiAds");
            var collection = _mongo.GetCollection<Penalty>(_client);
            var penalties = await collection.GetPenaltiesAsync(guild.Id);
            var blocks = await _mongo.GetCollection<Block>(_client).GetGuildBlocksAsync(guild.Id);
            var newPenalties = new List<Penalty>();

            if (penalties.All(p => p.PenaltyId != 1))
            {
                newPenalties.Add(new Penalty(guild.Id, 1, PenaltyType.Nothing, 1));
                logger.Info("Adding default info message penalty.");
            }
            if (penalties.All(p => p.PenaltyId != 2))
            {
                newPenalties.Add(new Penalty(guild.Id, 2, PenaltyType.Warn, 3));
                logger.Info("Adding default warn message penalty.");
            }
            if (penalties.All(p => p.PenaltyId != 3))
            {
                newPenalties.Add(new Penalty(guild.Id, 3, PenaltyType.Kick, 5));
                logger.Info("Adding default kick penalty.");
            }
            if (penalties.All(p => p.PenaltyId != 4))
            {
                newPenalties.Add(new Penalty(guild.Id, 4, PenaltyType.Ban, 6));
                logger.Info("Adding default ban penalty.");
            }

            if (newPenalties.Any())
                await collection.InsertManyAsync(newPenalties);

            if (!blocks.Any())
            {
                try
                {
                    await guild.DefaultChannel.SendMessageAsync(
                        "Thank you for inviting NAH. Please note that I'm currently in an Inactive state.\n" +
                        $"Please head over to github for documentations & a quickstart guide how to enable me.*({_config.CommandStrings.First()}github)*\n" +
                        "I've automatically added the default Penalties please change them to your needs!");
                    logger.Info($"Sent Joinmessage in {guild}/{guild.DefaultChannel}");
                }
                catch (Exception e)
                {
                    logger.Warn(e, $"Failed to send Joinmessage in {guild}/{guild.DefaultChannel}");
                }

            }
            
        }

        private static async Task<IScheduler> StartQuartz()
        {
            var factory = new StdSchedulerFactory();
            var scheduler = await factory.GetScheduler();
            await scheduler.Start();
            return scheduler;
        }

        private MongoClient CreateDatabaseConnection()
        {
            _logger.Info("Connecting to Mongo Database");
            return new MongoClient(_config.Database.ConnectionString);
        }

        private IServiceProvider ConfigureServices()
        {
            _logger.Info("Configuring dependency injection and services...");
            var servies = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_config)
                .AddSingleton(_mongo)
                .AddSingleton(_scheduler)
                .AddSingleton(new FaqSystem(_client, _mongo))
                .AddSingleton(new InteractiveService(_client))
                .AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false, LogLevel = LogSeverity.Verbose, DefaultRunMode = RunMode.Sync}));

            var provider = servies.BuildServiceProvider();
            return provider;
        }

        private Task ClientLogger(LogMessage message)
        {
            if (message.Exception == null)
                _discordLogger.Log(LogLevelParser(message.Severity), message.Message);
            else
                _discordLogger.Log(LogLevelParser(message.Severity), message.Exception, message.Message);

            return Task.CompletedTask;
        }

        public static LogLevel LogLevelParser(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Debug:
                    return LogLevel.Trace;

                case LogSeverity.Verbose:
                    return LogLevel.Debug;

                case LogSeverity.Info:
                    return LogLevel.Info;

                case LogSeverity.Warning:
                    return LogLevel.Warn;

                case LogSeverity.Error:
                    return LogLevel.Error;

                case LogSeverity.Critical:
                    return LogLevel.Fatal;

                default:
                    return LogLevel.Off;
            }
        }
    }
}