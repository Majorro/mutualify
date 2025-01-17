﻿using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Mutualify.Jobs.Interfaces;
using Mutualify.Repositories.Interfaces;
using Mutualify.Services.Interfaces;

namespace Mutualify.Jobs;

public class UserUpdateJob : IUserUpdateJob
{
    private readonly IUserRepository _userRepository;
    private readonly IUsersService _usersService;
    private readonly IRelationsService _relationsService;
    private readonly ILogger<UserUpdateJob> _logger;

    private const int _interval = 3; // seconds

    private static bool _isRunning = false;
    private static DateTime _lastStartDate;

    public UserUpdateJob(IUsersService usersService, IRelationsService relationsService, IUserRepository userRepository, ILogger<UserUpdateJob> logger)
    {
        _usersService = usersService;
        _relationsService = relationsService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Run(PerformContext context, IJobCancellationToken token)
    {
        var jobId = context.BackgroundJob.Id;

        _logger.LogInformation("[{JobId}] Starting user update job...", jobId);

        if (_isRunning && _lastStartDate.AddDays(2) > DateTime.Now)
        {
            _logger.LogInformation("[{JobId}] Job is already running, abort!", jobId);
            return;
        }

        _isRunning = true;
        _lastStartDate = DateTime.Now;

        var userUpdateQueue = await _userRepository.GetUsersForUpdateJob();

        for (var i = 0; i < userUpdateQueue.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            var userId = userUpdateQueue[i];
            var startTime = DateTime.Now;

            try
            {
#if !DEBUG
                if (i % 100 == 0)
#endif
                    _logger.LogInformation("[{JobId}] ({Current}/{Total}) Updating {Id}...", jobId, i+1,
                        userUpdateQueue.Count, userId);

                await _usersService.Update(userId);
                await _relationsService.UpdateRelations(userId);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is HttpRequestException)
                {
                    // don't fail on HttpRequestExceptions, just keep going
                    continue;
                }

                _isRunning = false;

                throw;
            }
            catch (DbUpdateConcurrencyException) { } // don't fail on HttpRequestExceptions or DbUpdateConcurrencyException, just keep going
            catch (HttpRequestException) { }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{JobId}] User update job has been cancelled!", jobId);

                _isRunning = false;
                return;
            }
            finally
            {
                var endTime = DateTime.Now;

                // yea its not really accurate but we don't need precision, just run it approximately every X seconds
                var elapsed = endTime - startTime;
                var timeout = elapsed.TotalSeconds < _interval ? _interval - (int) elapsed.TotalSeconds : 0;

                Thread.Sleep(timeout * 1000);
            }
        }

        _isRunning = false;
        _logger.LogInformation("[{JobId}] Finished user update job", jobId);
    }
}
