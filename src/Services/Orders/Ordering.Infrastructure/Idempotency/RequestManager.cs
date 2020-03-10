﻿using Ordering.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ordering.Infrastructure.Idempotency
{
    public class RequestManager : IRequestManager
    {
        private readonly OrderingContext _context;

        public RequestManager(OrderingContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }
        public async Task<bool> ExistsAsync(Guid id)
        {
            var request = await _context.FindAsync<ClientRequest>(id);
            return request != null;
        }

        public async Task CreateRequestforCommandAsync<T>(Guid id)
        {
            var exists = await ExistsAsync(id);

            var request = exists
                ? throw new OrderingDomainException($"Request with {id} already exists")
                : new ClientRequest()
                {
                    Id = id,
                    Name = typeof(T).Name,
                    Time = DateTime.Now
                };

            _context.Add(request);
            await _context.SaveChangesAsync();
        }
    }
}
