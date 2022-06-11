﻿using Mutualify.Database.Models;

namespace Mutualify.Services.Interfaces;

public interface IRelationsService
{
    Task<List<User>> GetFriends(int userId);
    Task<List<User>> GetFollowers(int userId);
    Task UpdateRelations(int userId);
}