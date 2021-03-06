using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using OpenGL;

namespace NeutrinoGl
{
	public class RenderBuffers : Neutrino.RenderBuffer
	{
		Context context_;

		class TexChannel
		{
			public uint dimensions_;
			public uint id_;
			public float[] data_;
		}

		uint positionsId_;
		Neutrino._math.vec3[] positions_;

		uint colorsId_;
		uint[] colors_;

		TexChannel[] texChannels_;

		uint indicesId_;
		IntPtr indicesData_;

		uint numVertices_;

		Neutrino.RenderCall[] renderCalls_;
		uint numRenderCalls_;

		public RenderBuffers(Context context)
		{
			context_ = context;
		}

		public void initialize(uint maxNumVertices, uint[] texChannels, ushort[] indices, uint maxNumRenderCalls)
		{
			{
				uint positionsDataSize = (uint)(maxNumVertices * Marshal.SizeOf(typeof(Neutrino._math.vec3)));
				positionsId_ = Gl.GenBuffer();
				positions_ = new Neutrino._math.vec3[maxNumVertices];

				Gl.BindBuffer(BufferTarget.ArrayBuffer, positionsId_);
				Gl.BufferData(BufferTarget.ArrayBuffer, (IntPtr)positionsDataSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
			}

			{
				uint colorsDataSize = (uint)(maxNumVertices * Marshal.SizeOf(typeof(uint)));
				colorsId_ = Gl.GenBuffer();
				colors_ = new uint[maxNumVertices];

				Gl.BindBuffer(BufferTarget.ArrayBuffer, colorsId_);
				Gl.BufferData(BufferTarget.ArrayBuffer, (IntPtr)colorsDataSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
			}

			texChannels_ = new TexChannel[texChannels.Length];

			for (uint texIndex = 0; texIndex < texChannels.Length; ++texIndex)
			{
				TexChannel texChannel = new TexChannel();
				uint texDataSize = (uint)(maxNumVertices * texChannels[texIndex] * Marshal.SizeOf(typeof(float)));
				texChannel.dimensions_ = texChannels[texIndex];
				texChannel.id_ = Gl.GenBuffer();
				texChannel.data_ = new float[maxNumVertices * texChannels[texIndex]];

				Gl.BindBuffer(BufferTarget.ArrayBuffer, texChannel.id_);
				Gl.BufferData(BufferTarget.ArrayBuffer, (IntPtr)texDataSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

				texChannels_[texIndex] = texChannel;
			}

			{
				uint indicesDataSize = (uint)(indices.Length * Marshal.SizeOf(typeof(ushort)));
				indicesId_ = Gl.GenBuffer();
				indicesData_ = Marshal.AllocHGlobal((int)indicesDataSize);

				{
					IntPtr ptr = indicesData_;
					for (uint i = 0; i < indices.Length; ptr = IntPtr.Add(ptr, Marshal.SizeOf(typeof(ushort))), ++i)
					{
						Marshal.WriteInt16(ptr, (short)indices[i]);
					}
				}

				Gl.BindBuffer(BufferTarget.ElementArrayBuffer, indicesId_);
				Gl.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)indicesDataSize, indicesData_, BufferUsageHint.StaticDraw);
			}

			numVertices_ = 0;

			renderCalls_ = new Neutrino.RenderCall[maxNumRenderCalls];
			numRenderCalls_ = 0;
		}

		public void pushVertex(ref Neutrino.RenderVertex v)
		{
			positions_[numVertices_] = v.position_;
			colors_[numVertices_] = v.color_;

			uint texIndex = 0;
			foreach (TexChannel texChannel in texChannels_)
			{
				uint destIndex = numVertices_ * texChannel.dimensions_;
				 
				foreach (float src in v.texCoords_[texIndex])
					texChannel.data_[destIndex++] = src;

				++texIndex;

				// next two variants are too slow

				//v.texCoords_[texIndex++].CopyTo(texChannel.data_, numVertices_ * texChannel.dimensions_);
				
				//Array.Copy(v.texCoords_[texIndex++], 0, texChannel.data_, numVertices_ * texChannel.dimensions_,
				//	texChannel.dimensions_);
			}

			++numVertices_;
		}

		public void pushRenderCall(ref Neutrino.RenderCall rc)
		{
			renderCalls_[numRenderCalls_++] = rc;
		}

		public void cleanup()
		{
			numVertices_ = 0;
			numRenderCalls_ = 0;
		}

		public void updateGlBuffers()
		{
			{
				GCHandle handle = GCHandle.Alloc(positions_, GCHandleType.Pinned);
				IntPtr pointer = handle.AddrOfPinnedObject();

				Gl.BindBuffer(BufferTarget.ArrayBuffer, positionsId_);
				Gl.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)(numVertices_ * Marshal.SizeOf(typeof(Neutrino._math.vec3))), pointer);

				handle.Free();
			}

			{
				GCHandle handle = GCHandle.Alloc(colors_, GCHandleType.Pinned);
				IntPtr pointer = handle.AddrOfPinnedObject();

				Gl.BindBuffer(BufferTarget.ArrayBuffer, colorsId_);
				Gl.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)(numVertices_ * Marshal.SizeOf(typeof(uint))), pointer);

				handle.Free();
			}

			for (uint texIndex = 0; texIndex < texChannels_.Length; ++texIndex)
			{
				TexChannel texChannel = texChannels_[texIndex];

				GCHandle handle = GCHandle.Alloc(texChannel.data_, GCHandleType.Pinned);
				IntPtr pointer = handle.AddrOfPinnedObject();

				Gl.BindBuffer(BufferTarget.ArrayBuffer, texChannel.id_);
				Gl.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, 
					(IntPtr)(numVertices_ * texChannel.dimensions_ * Marshal.SizeOf(typeof(float))), pointer);

				handle.Free();
			}
		}

		public void bind()
		{
			Materials materials = context_.materials();

			Gl.EnableVertexAttribArray((uint)materials.positionAttribLocation());
			Gl.BindBuffer(BufferTarget.ArrayBuffer, positionsId_);
			Gl.VertexAttribPointer((uint)materials.positionAttribLocation(), 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);

			Gl.EnableVertexAttribArray((uint)materials.colorAttribLocation());
			Gl.BindBuffer(BufferTarget.ArrayBuffer, colorsId_);
			Gl.VertexAttribPointer((uint)materials.colorAttribLocation(), 4, VertexAttribPointerType.UnsignedByte, true, 0, IntPtr.Zero);

			if (texChannels_.Length > 0)
			{
				// only one texture channel currently supported by NeutrinoParticles
				Debug.Assert(texChannels_.Length == 1);

				Gl.EnableVertexAttribArray((uint)materials.tex0AttribLocation());
				Gl.BindBuffer(BufferTarget.ArrayBuffer, texChannels_[0].id_);
				Gl.VertexAttribPointer((uint)materials.tex0AttribLocation(), (int)texChannels_[0].dimensions_,
					VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
			}

			Gl.BindBuffer(BufferTarget.ElementArrayBuffer, indicesId_);
		}

		public void shutdown()
		{
			Gl.DeleteBuffer(positionsId_);
			Gl.DeleteBuffer(colorsId_);
			Gl.DeleteBuffer(indicesId_);

			for (uint texIndex = 0; texIndex < texChannels_.Length; ++texIndex)
			{
				Gl.DeleteBuffer(texChannels_[texIndex].id_);
			}
		}

		public Neutrino.RenderCall[] renderCalls() { return renderCalls_; }
		public uint numRenderCalls() { return numRenderCalls_; }
	}
}