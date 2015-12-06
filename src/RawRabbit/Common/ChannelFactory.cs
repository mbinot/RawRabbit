﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using RabbitMQ.Client;

namespace RawRabbit.Common
{
	public interface IChannelFactory : IDisposable
	{
		/// <summary>
		/// Retrieves a channel that is disposed by the channel factory
		/// </summary>
		/// <returns>A new or existing instance of an IModel</returns>
		IModel GetChannel();
		/// <summary>
		/// Creates a new istance of a channal that the caller is responsible
		/// in closing and disposing.
		/// </summary>
		/// <returns>A new instance of an IModel</returns>
		IModel CreateChannel();
	}

	public class ChannelFactory : IChannelFactory
	{
		private readonly IConnectionBroker _connectionBroker;
		private readonly ConcurrentDictionary<IConnection, ThreadLocal<IModel>> _connectionToChannel;

		public ChannelFactory(IConnectionBroker connectionBroker)
		{
			_connectionBroker = connectionBroker;
			_connectionToChannel = new ConcurrentDictionary<IConnection, ThreadLocal<IModel>>();
		}

		public void Dispose()
		{
			_connectionBroker?.Dispose();
		}

		public void CloseAll()
		{
			foreach (var connection in _connectionToChannel.Keys)
			{
				connection?.Close();
			}
		}

		public IModel GetChannel()
		{
			var currentConnection = _connectionBroker.GetConnection();
			if (!_connectionToChannel.ContainsKey(currentConnection))
			{
				_connectionToChannel.TryAdd(currentConnection, new ThreadLocal<IModel>(currentConnection.CreateModel));
			}
			
			var threadChannel = _connectionToChannel[currentConnection];
			if (threadChannel.Value.IsOpen)
			{
				return threadChannel.Value;
			}
			
			threadChannel.Value?.Dispose();
			threadChannel.Value = _connectionBroker.GetConnection().CreateModel();
			
			return threadChannel.Value;
		}

		public IModel CreateChannel()
		{
			return _connectionBroker.GetConnection().CreateModel();
		}
	}
}
