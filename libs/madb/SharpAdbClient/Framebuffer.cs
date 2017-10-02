// <copyright file="Framebuffer.cs" company="The Android Open Source Project, Ryan Conrad, Quamotion">
// Copyright (c) The Android Open Source Project, Ryan Conrad, Quamotion. All rights reserved.
// </copyright>

using System.Net; 

namespace SharpAdbClient
{
    using System;
#if !NETFX
    using System.Buffers;
#endif
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides access to the framebuffer (that is, a copy of the image being displayed on the device screen).
    /// </summary>
    public class Framebuffer : IDisposable
    {
        private readonly AdbClient client;
        private byte[] headerData;
        private bool headerInitialized;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Framebuffer"/> class.
        /// </summary>
        /// <param name="device">
        /// The device for which to fetch the frame buffer.
        /// </param>
        /// <param name="client">
        /// A <see cref="AdbClient"/> which manages the connection with adb.
        /// </param>
        public Framebuffer(IAdbDeviceData device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }
            this.Device = device;

            // Initialize the headerData buffer
            var size = Marshal.SizeOf<FramebufferHeader>();
            this.headerData = new byte[size];
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Framebuffer"/> class.
        /// </summary>
        /// <param name="device">
        /// The device for which to fetch the frame buffer.
        /// </param>
        /// <param name="client">
        /// A <see cref="AdbClient"/> which manages the connection with adb.
        /// </param>
        public Framebuffer(DeviceData device, AdbClient client)
            : this(device)
        { 

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            this.client = client;
        }

        /// <summary>
        /// Gets the device for which to fetch the frame buffer.
        /// </summary>
        public IAdbDeviceData Device
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the framebuffer header. The header contains information such as the width and height and the color encoding.
        /// This property is set after you call <see cref="RefreshAsync(CancellationToken)"/>.
        /// </summary>
        public FramebufferHeader Header
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the framebuffer data. You need to parse the <see cref="FramebufferHeader"/> to interpret this data (such as the color encoding).
        /// This property is set after you call <see cref="RefreshAsync(CancellationToken)"/>.
        /// </summary>
        public byte[] Data
        {
            get;
            private set;
        }

        /// <summary>
        /// Asynchronously refreshes the framebuffer: fetches the latest framebuffer data from the device. Access the <see cref="Header"/>
        /// and <see cref="Data"/> properties to get the updated framebuffer data.
        /// </summary>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> which can be used to cancel the asynchronous task.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> which represents the asynchronous operation.
        /// </returns>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            this.EnsureNotDisposed();
            EndPoint endpoint = null;
            if (this.Device.GetType() == typeof(DeviceData))
            {
                endpoint = this.client.EndPoint;
            }
            else
            {
                throw new NotImplementedException();
            }
            using (var socket = Factories.AdbSocketFactory(this.client.EndPoint))
            {
                // Select the target device
                this.client.SetDevice(socket, this.Device);

                // Send the framebuffer command
                socket.SendAdbRequest("framebuffer:");
                socket.ReadAdbResponse();

                // The result first is a FramebufferHeader object,
                await socket.ReadAsync(this.headerData, cancellationToken).ConfigureAwait(false);

                if (!this.headerInitialized)
                {
                    this.Header = FramebufferHeader.Read(this.headerData);
                    this.headerInitialized = true;
                }

                if (this.Data == null || this.Data.Length < this.Header.Size)
                {
#if !NETFX
                    // Optimization on .NET Core: Use the BufferPool to rent buffers
                    if (this.Data != null)
                    {
                        ArrayPool<byte>.Shared.Return(this.Data, clearArray: false);
                    }

                    this.Data = ArrayPool<byte>.Shared.Rent((int)this.Header.Size);
#else
                    this.Data = new byte[(int)this.Header.Size];
#endif
                }

                // followed by the actual framebuffer content
                await socket.ReadAsync(this.Data, (int)this.Header.Size, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Converts the framebuffer data to a <see cref="Image"/>.
        /// </summary>
        /// <returns>
        /// An <see cref="Image"/> which represents the framebuffer data.
        /// </returns>
        public System.Drawing.Image ToImage()
        {
            this.EnsureNotDisposed();

            if (this.Data == null)
            {
                throw new InvalidOperationException("Call RefreshAsync first");
            }

            return this.Header.ToImage(this.Data);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
#if !NETFX
            if (this.Data != null)
            {
                ArrayPool<byte>.Shared.Return(this.Data, clearArray: false);
            }
#endif

            this.headerData = null;
            this.headerInitialized = false;
            this.disposed = true;
        }

        /// <summary>
        /// Throws an exception if this <see cref="Framebuffer"/> has been disposed.
        /// </summary>
        protected void EnsureNotDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(Framebuffer));
            }
        }
    }
}
