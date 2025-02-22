﻿using DailyLeetcodeReminder.Domain.Entities;
using DailyLeetcodeReminder.Domain.Enums;
using DailyLeetcodeReminder.Domain.Exceptions;
using DailyLeetcodeReminder.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyLeetcodeReminder.Infrastructure.Repositories;

public class ChallengerRepository : IChallengerRepository
{
    private readonly ApplicationDbContext applicationDbContext;

    public ChallengerRepository(ApplicationDbContext applicationDbContext)
    {
        this.applicationDbContext = applicationDbContext;
    }

    public async Task<Challenger> SelectUserByTelegramIdAsync(long telegramId)
    {
        return await this.applicationDbContext
            .Set<Challenger>()
            .FirstOrDefaultAsync(x => x.TelegramId == telegramId);
    }

    public async Task<Challenger> SelectUserByLeetcodeUsernameAsync(string leetcodeUsername)
    {
        return await this.applicationDbContext
            .Set<Challenger>()
            .FirstOrDefaultAsync(x => x.LeetcodeUserName == leetcodeUsername);
    }

    public async Task<Challenger> InsertChallengerAsync(Challenger challenger)
    {
        var userEntityEntry = await this.applicationDbContext
            .Set<Challenger>()
            .AddAsync(challenger);

        try
        {
            await this.applicationDbContext
                .SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is not PostgresException pgException)
            {
                throw;
            }

            if (pgException.SqlState == "23505" &&
                    pgException.Message.Contains("PK_Challengers"))
            {
                throw new AlreadyExistsException(challenger.LeetcodeUserName);
            }

            if (pgException.SqlState == "23505" &&
                     pgException.Message.Contains("IX_Challengers_LeetcodeUserName"))
            {
                throw new DuplicateException(challenger.LeetcodeUserName);
            }
        }

        return userEntityEntry.Entity;
    }

    public async Task UpdateChallengerAsync(Challenger challenger)
    {
        this.applicationDbContext
            .Set<Challenger>()
            .Update(challenger);

        await this.SaveChangesAsync();
    }

    public async Task<List<ChallengerWithNoAttempt>> SelectUsersHasNoAttemptsAsync()
    {
        var today = DateTime.Now.Date;
        
        return await this.applicationDbContext
            .Set<Challenger>()
            .Include(ch => ch.DailyAttempts
                .Where(da => da.Date == today)
                .Where(da => da.SolvedProblems == 0))
            .Where(ch => ch.Status == UserStatus.Active)
            .Select(ch => new ChallengerWithNoAttempt
            {
                LeetcodeUserName = ch.LeetcodeUserName,
                TelegramId = ch.TelegramId,
                TotalSolvedProblems = ch.TotalSolvedProblems
            })
            .ToListAsync();
    }

    public async Task<List<Challenger>> SelectActiveChallengersWithAttemptsAsync()
    {
        var yesterDay = DateTime.Now.Date.AddDays(-1);

        return await this.applicationDbContext
            .Set<Challenger>()
            .Include(ch => ch.DailyAttempts
                .Where(da => da.Date == yesterDay))
            .Where(ch => ch.Status == UserStatus.Active)
            .ToListAsync();
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return await this.applicationDbContext
            .SaveChangesAsync(cancellationToken);
    }

    public async Task<Challenger> SelectUserWithWeeklyAttempts(long userId)
    {
        var lastWeek = DateTime.Now.Date.AddDays(-8);
        var today = DateTime.Now.Date;

        return await this.applicationDbContext
            .Set<Challenger>()
            .Include(user => user.DailyAttempts
                .Where(dailyAttempt => dailyAttempt.Date >= lastWeek &&
                    dailyAttempt.Date != today))
            .Where(user => user.TelegramId == userId)
            .FirstAsync();
    }

    public async Task<List<Challenger>> SelectActiveChallengers()
    {
        return await this.applicationDbContext
            .Set<Challenger>()
            .Where(ch => ch.Status == UserStatus.Active)
            .ToListAsync();
    }
}