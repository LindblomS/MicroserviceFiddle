﻿using MediatR;
using Microsoft.Extensions.Logging;
using Ordering.API.Application.IntegrationEvents;
using Ordering.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EventBus.Extensions;
using Serilog.Context;

namespace Ordering.API.Application.Behaviours
{
    public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
        private readonly OrderingContext _orderingContext;
        private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;

        public TransactionBehavior(
            ILogger<TransactionBehavior<TRequest, TResponse>> logger,
            OrderingContext orderingContext,
            IOrderingIntegrationEventService orderingIntegrationEventService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderingContext = orderingContext ?? throw new ArgumentNullException(nameof(orderingContext));
            _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            var respone = default(TResponse);
            var typeName = request.GetGenericTypeName();

            try
            {
                if (_orderingContext.HasActiveTransaction())
                {
                    return await next();
                }

                var strategy = _orderingContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    Guid transactionId;

                    using (var transaction = await _orderingContext.BeginTransactionAsync())
                    using (LogContext.PushProperty("TransactionContext", transaction.TransactionId))
                    {
                            _logger.LogInformation($"----- Begin transaction {transaction.TransactionId} for {typeName} ({request})");

                            respone = await next();

                            _logger.LogInformation($"----- Commit transaction {transaction.TransactionId} for {typeName}");

                            await _orderingContext.CommitTransactionAsync(transaction);

                            transactionId = transaction.TransactionId;
                    }

                    await _orderingIntegrationEventService.PublishEventsThroughEventBusAsync(transactionId);

                });

                return respone;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ERROR Handling transaction for {typeName} ({request})");
                throw;
            }
        }
    }
}
