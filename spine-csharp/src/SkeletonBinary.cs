using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace Spine
{
	public class SkeletonBinary
	{
		private const int TimelineScale = 0;
		private const int TimelineRotate = 1;
		private const int TimelineTranslate = 2;
		private const int TimelineAttachment = 3;
		private const int TimelineColor = 4;
		private const int TimelineFlipx = 5;
		private const int TimelineFlipy = 6;

		private const int CurveLinear = 0;
		private const int CurveStepped = 1;
		private const int CurveBezier = 2;

		private AttachmentLoader _attachmentLoader;
		private float _scale = 1;
		private bool _nonEssential;

		public SkeletonBinary(Atlas atlas)
		{
			_attachmentLoader = new AtlasAttachmentLoader(atlas);
		}

		public SkeletonBinary(AttachmentLoader attachmentLoader)
		{
			this._attachmentLoader = attachmentLoader;
		}

		/** Scales the bones, images, and animations as they are loaded. */
		public float Scale
		{
			get { return _scale; }
			set { _scale = value; }
		}

		public SkeletonData ReadSkeletonData(FileStream file)
		{
			if (file == null)
				throw new ArgumentException("file stream cannot be null.");

			return ReadSkeletonData(file, Path.GetFileNameWithoutExtension(file.Name));
		}

		public SkeletonData ReadSkeletonData(Stream stream, string skeletonName)
		{
			if (stream == null)
				throw new ArgumentException("file stream cannot be null.");

			var scale = _scale;

			var skeletonData = new SkeletonData { name = skeletonName };

			var reader = new BigEndianBinaryReader(stream);

			try
			{
				var hash = reader.ReadString();
				var version = reader.ReadString();
				var width = reader.ReadSingle();
				var height = reader.ReadSingle();
				//				skeletonData.hash = reader.ReadString();
				//				if (skeletonData.hash.isEmpty())
				//					skeletonData.hash = null;
				//
				//				skeletonData.version = reader.ReadString();
				//				if (skeletonData.version.isEmpty())
				//					skeletonData.version = null;
				//
				//				skeletonData.width = reader.ReadSingle();
				//				skeletonData.height = reader.ReadSingle();

				_nonEssential = reader.ReadBoolean();

				if (_nonEssential)
				{
					reader.ReadString();
//					skeletonData.imagesPath = reader.ReadString();
//					if (skeletonData.imagesPath.isEmpty())
//						skeletonData.imagesPath = null;
				}

				// Bones.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
				{
					var name = reader.ReadString();
					BoneData parent = null;
					var parentIndex = reader.ReadInt(true) - 1;
					if (parentIndex != -1)
						parent = skeletonData.bones[parentIndex];

					var boneData = new BoneData(name, parent)
					{
						x = reader.ReadSingle() * scale,
						y = reader.ReadSingle() * scale,
						scaleX = reader.ReadSingle(),
						scaleY = reader.ReadSingle(),
						rotation = reader.ReadSingle(),
						length = reader.ReadSingle() * scale
					};

					reader.ReadBoolean();	// flipX
					reader.ReadBoolean();	// flipY

					boneData.InheritScale = reader.ReadBoolean();
					boneData.InheritRotation = reader.ReadBoolean();
					//				if (nonEssential) 
					//					Color.rgba8888ToColor(boneData.color, reader.ReadInt32());
					skeletonData.bones.Add(boneData);
				}

				// IK constraints.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
				{
					reader.ReadString();		//IkConstraintData ikConstraintData = new IkConstraintData(reader.readString());
					for (int ii = 0, nn = reader.ReadInt(true); ii < nn; ii++)
						reader.ReadInt(true);	//ikConstraintData.bones.add(skeletonData.bones.get(reader.readInt(true))));
					reader.ReadInt(true);//ikConstraintData.target = skeletonData.bones.get(reader.readInt(true)));
					reader.ReadSingle();//ikConstraintData.mix = reader.readFloat();
					reader.ReadByte();//ikConstraintData.bendDirection = reader.readByte();
					//					skeletonData.ikConstraints.add(ikConstraintData);
				}

				// Slots.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
				{
					var slotName = reader.ReadString();
					var boneData = skeletonData.bones[reader.ReadInt(true)];
					var colour = reader.ReadBytes(4);
					var slotData = new SlotData(slotName, boneData)
					{
						R = colour[0] / (float)255,
						G = colour[1] / (float)255,
						B = colour[2] / (float)255,
						A = colour[3] / (float)255,
						attachmentName = reader.ReadString(),
						additiveBlending = reader.ReadBoolean()
					};
					skeletonData.slots.Add(slotData);
				}

				// Default skin.
				var defaultSkin = ReadSkin(reader, "default");
				if (defaultSkin != null)
				{
					skeletonData.defaultSkin = defaultSkin;
					skeletonData.skins.Add(defaultSkin);
				}

				// Skins.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
					skeletonData.skins.Add(ReadSkin(reader, reader.ReadString()));

				// Events.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
				{
					var eventData = new EventData(reader.ReadString())
					{
						Int = reader.ReadInt(false),
						Float = reader.ReadSingle(),
						String = reader.ReadString()
					};
					skeletonData.events.Add(eventData);
				}

				// Animations.
				for (int i = 0, n = reader.ReadInt(true); i < n; i++)
					ReadAnimation(reader.ReadString(), reader, skeletonData);
			}
			catch (IOException ex)
			{
				throw new SerializationException("Error reading skeleton file.", ex);
			}
			finally
			{
				try
				{
					reader.Close();
				}
				catch (IOException)
				{
				}
			}

			//		skeletonData.bones.shrink();
			//		skeletonData.slots.shrink();
			//		skeletonData.skins.shrink();
			return skeletonData;
		}

		private Skin ReadSkin(BigEndianBinaryReader input, String skinName)
		{
			var slotCount = input.ReadInt(true);
			if (slotCount == 0)
				return null;

			var skin = new Skin(skinName);

			for (var i = 0; i < slotCount; i++)
			{
				var slotIndex = input.ReadInt(true);
				for (int ii = 0, nn = input.ReadInt(true); ii < nn; ii++)
				{
					var name = input.ReadString();
					skin.AddAttachment(slotIndex, name, ReadAttachment(input, skin, name));
				}
			}
			return skin;
		}

		private Attachment ReadAttachment(BigEndianBinaryReader reader, Skin skin, string attachmentName)
		{
			var scale = _scale;

			var name = reader.ReadString() ?? attachmentName;
			
			switch ((AttachmentType)reader.ReadByte())
			{
				case AttachmentType.region:
					{
						var path = reader.ReadString() ?? name;
						var region = _attachmentLoader.NewRegionAttachment(skin, name, path);
						if (region == null) return null;
						region.Path = path;
						region.X = reader.ReadSingle() * scale;
						region.Y = reader.ReadSingle() * scale;
						region.ScaleX = reader.ReadSingle();
						region.ScaleY = reader.ReadSingle();
						region.Rotation = reader.ReadSingle();
						region.Width = reader.ReadSingle() * scale;
						region.Height = reader.ReadSingle() * scale;
						region.R = reader.ReadByte() / (float)255;
						region.G = reader.ReadByte() / (float)255;
						region.B = reader.ReadByte() / (float)255;
						region.A = reader.ReadByte() / (float)255;
						region.UpdateOffset();
						return region;
					}
				case AttachmentType.boundingbox:
					{
						var box = _attachmentLoader.NewBoundingBoxAttachment(skin, name);
						if (box == null) return null;
						box.Vertices = ReadFloatArray(reader, scale);
						return box;
					}
				case AttachmentType.mesh:
					{
						var path = reader.ReadString() ?? name;

						var mesh = _attachmentLoader.NewMeshAttachment(skin, name, path);
						if (mesh == null) return null;
						mesh.Path = path;
						var uvs = ReadFloatArray(reader, 1);
						var triangles = ReadShortArray(reader);
						var vertices = ReadFloatArray(reader, scale);
						var colour = reader.ReadBytes(4);
						mesh.Vertices = vertices;
						mesh.Triangles = triangles;
						mesh.RegionUVs = uvs;
						mesh.R = colour[0] / (float)255;
						mesh.G = colour[1] / (float)255;
						mesh.B = colour[2] / (float)255;
						mesh.A = colour[3] / (float)255;
						mesh.UpdateUVs();

						mesh.HullLength = reader.ReadInt(true) * 2;

						if (_nonEssential)
						{
							mesh.Edges = ReadIntArray(reader);
							mesh.Width = reader.ReadSingle() * scale;
							mesh.Height = reader.ReadSingle() * scale;
						}
						return mesh;
					}
				case AttachmentType.skinnedmesh:
					{
						var path = reader.ReadString() ?? name;

						var mesh = _attachmentLoader.NewSkinnedMeshAttachment(skin, name, path);
						if (mesh == null) return null;
						mesh.Path = path;
						var uvs = ReadFloatArray(reader, 1);
						int[] triangles = ReadShortArray(reader);

						var vertexCount = reader.ReadInt(true);
						var weights = new List<float>(uvs.Length * 3 * 3);
						var bones = new List<int>(uvs.Length * 3);

						for (var i = 0; i < vertexCount; i++)
						{
							var boneCount = (int)reader.ReadSingle();
							bones.Add(boneCount);

							for (var nn = i + boneCount * 4; i < nn; i += 4)
							{
								bones.Add((int)reader.ReadSingle());
								weights.Add(reader.ReadSingle() * scale);
								weights.Add(reader.ReadSingle() * scale);
								weights.Add(reader.ReadSingle());
							}
						}
						mesh.Bones = bones.ToArray();
						mesh.Weights = weights.ToArray();
						mesh.Triangles = triangles;
						mesh.RegionUVs = uvs;
						mesh.UpdateUVs();

						var colour = reader.ReadBytes(4);
						mesh.R = colour[0] / (float)255;
						mesh.G = colour[1] / (float)255;
						mesh.B = colour[2] / (float)255;
						mesh.A = colour[3] / (float)255;

						mesh.HullLength = reader.ReadInt(true) * 2;

						if (_nonEssential)
						{
							mesh.Edges = ReadIntArray(reader);
							mesh.Width = reader.ReadSingle() * scale;
							mesh.Height = reader.ReadSingle() * scale;
						}
						return mesh;
					}
			}
			return null;
		}

		private float[] ReadFloatArray(BigEndianBinaryReader input, float scale)
		{
			var n = input.ReadInt(true);
			var array = new float[n];
			if (scale == 1)
			{
				for (var i = 0; i < n; i++)
					array[i] = input.ReadSingle();
			}
			else
			{
				for (int i = 0; i < n; i++)
					array[i] = input.ReadSingle() * scale;
			}
			return array;
		}

		private int[] ReadIntArray(BigEndianBinaryReader input)
		{
			var n = input.ReadInt(true);
			var array = new int[n];
			for (var i = 0; i < n; i++)
				array[i] = input.ReadInt(true);

			return array;
		}

		private int[] ReadShortArray(BigEndianBinaryReader input)
		{
			var n = input.ReadInt(true);
			var array = new int[n];
			for (var i = 0; i < n; i++)
				array[i] = input.ReadInt16();

			return array;
		}

		private void ReadAnimation(String name, BigEndianBinaryReader input, SkeletonData skeletonData)
		{
			var timelines = new List<Timeline>();
			var scale = _scale;
			float duration = 0;

			try
			{
				// Slot timelines.
				for (int i = 0, n = input.ReadInt(true); i < n; i++)
				{
					var slotIndex = input.ReadInt(true);
					for (int ii = 0, nn = input.ReadInt(true); ii < nn; ii++)
					{
						int timelineType = input.ReadByte();
						var frameCount = input.ReadInt(true);
						switch (timelineType)
						{
							case TimelineColor:
								{
									var colorTimeline = new ColorTimeline(frameCount) { slotIndex = slotIndex };
									for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
									{
										var time = input.ReadSingle();
										var colour = input.ReadBytes(4);
										colorTimeline.setFrame(frameIndex, time, colour[0] / (float)255, colour[1] / (float)255, colour[2] / (float)255, colour[3] / (float)255);
										if (frameIndex < frameCount - 1)
											ReadCurve(input, frameIndex, colorTimeline);
									}
									timelines.Add(colorTimeline);
									duration = Math.Max(duration, colorTimeline.Frames[frameCount * 5 - 5]);
									break;
								}
							case TimelineAttachment:
								var timeline = new AttachmentTimeline(frameCount) { slotIndex = slotIndex };
								for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
									timeline.setFrame(frameIndex, input.ReadSingle(), input.ReadString());
								timelines.Add(timeline);
								duration = Math.Max(duration, timeline.Frames[frameCount - 1]);
								break;
						}
					}
				}

				// Bone timelines.
				for (int i = 0, n = input.ReadInt(true); i < n; i++)
				{
					var boneIndex = input.ReadInt(true);
					for (int ii = 0, nn = input.ReadInt(true); ii < nn; ii++)
					{
						int timelineType = input.ReadByte();
						var frameCount = input.ReadInt(true);
						switch (timelineType)
						{
							case TimelineRotate:
								{
									var timeline = new RotateTimeline(frameCount) { BoneIndex = boneIndex };
									for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
									{
										timeline.SetFrame(frameIndex, input.ReadSingle(), input.ReadSingle());
										if (frameIndex < frameCount - 1)
											ReadCurve(input, frameIndex, timeline);
									}
									timelines.Add(timeline);
									duration = Math.Max(duration, timeline.Frames[frameCount * 2 - 2]);
									break;
								}
							case TimelineTranslate:
							case TimelineScale:
								{
									TranslateTimeline timeline;
									float timelineScale = 1;
									if (timelineType == TimelineScale)
										timeline = new ScaleTimeline(frameCount);
									else
									{
										timeline = new TranslateTimeline(frameCount);
										timelineScale = scale;
									}
									timeline.boneIndex = boneIndex;
									for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
									{
										timeline.SetFrame(frameIndex, input.ReadSingle(), input.ReadSingle() * timelineScale, input.ReadSingle() * timelineScale);
										if (frameIndex < frameCount - 1)
											ReadCurve(input, frameIndex, timeline);
									}
									timelines.Add(timeline);
									duration = Math.Max(duration, timeline.Frames[frameCount * 3 - 3]);
									break;
								}
							case TimelineFlipx:
							case TimelineFlipy:
								{
									//						FlipXTimeline timeline = timelineType == TIMELINE_FLIPX ? new FlipXTimeline(frameCount) : new FlipYTimeline(frameCount);
									//						timeline.boneIndex = boneIndex;
									for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
									{
										input.ReadSingle();
										input.ReadBoolean();
										//							timeline.setFrame(frameIndex, reader.readFloat(), reader.readBoolean());
									}
									//						timelines.add(timeline);
									//						duration = Math.max(duration, timeline.getFrames()[frameCount * 2 - 2]);
									break;
								}
						}
					}
				}

				// IK timelines.
				// this version of the runtime doesn't support IK but the data for it is still exported from Spine so just read through it and do nothing
				for (int i = 0, n = input.ReadInt(true); i < n; i++)
				{
					input.ReadInt(true);//IkConstraintData) ikConstraint = skeletonData.ikConstraints.get(reader.readInt(true));
					int frameCount = input.ReadInt(true);
//					IkConstraintTimeline timeline = new IkConstraintTimeline(frameCount);
//					timeline.ikConstraintIndex = skeletonData.getIkConstraints().indexOf(ikConstraint, true);
					for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
					{
						input.ReadSingle(); input.ReadSingle(); input.ReadByte();//						timeline.setFrame(frameIndex, reader.readFloat(), reader.readFloat(), reader.readByte());
						if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, null);
					}
//					timelines.add(timeline);
//					duration = Math.max(duration, timeline.getFrames()[frameCount * 3 - 3]);
				}

				// FFD timelines.
				for (int i = 0, n = input.ReadInt(true); i < n; i++)
				{
					var skin = skeletonData.skins[input.ReadInt(true)];

					for (int ii = 0, nn = input.ReadInt(true); ii < nn; ii++)
					{
						var slotIndex = input.ReadInt(true);
						for (int iii = 0, nnn = input.ReadInt(true); iii < nnn; iii++)
						{
							var attachment = skin.GetAttachment(slotIndex, input.ReadString());
							var frameCount = input.ReadInt(true);
							var timeline = new FFDTimeline(frameCount) { slotIndex = slotIndex, attachment = attachment };
							for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
							{
								var time = input.ReadSingle();

								float[] vertices;
								int vertexCount;
								if (attachment is MeshAttachment)
									vertexCount = ((MeshAttachment)attachment).Vertices.Length;
								else
									vertexCount = ((SkinnedMeshAttachment)attachment).Weights.Length / 3 * 2;

								var end = input.ReadInt(true);
								if (end == 0)
								{
									if (attachment is MeshAttachment)
										vertices = ((MeshAttachment)attachment).Vertices;
									else
										vertices = new float[vertexCount];
								}
								else
								{
									vertices = new float[vertexCount];
									var start = input.ReadInt(true);
									end += start;
									if (scale == 1)
									{
										for (var v = start; v < end; v++)
											vertices[v] = input.ReadSingle();
									}
									else
									{
										for (var v = start; v < end; v++)
											vertices[v] = input.ReadSingle() * scale;
									}
									if (attachment is MeshAttachment)
									{
										var meshVertices = ((MeshAttachment)attachment).Vertices;
										for (int v = 0, vn = vertices.Length; v < vn; v++)
											vertices[v] += meshVertices[v];
									}
								}

								timeline.setFrame(frameIndex, time, vertices);
								if (frameIndex < frameCount - 1) ReadCurve(input, frameIndex, timeline);
							}
							timelines.Add(timeline);
							duration = Math.Max(duration, timeline.Frames[frameCount - 1]);
						}
					}
				}

				// Draw order timeline.
				var drawOrderCount = input.ReadInt(true);
				if (drawOrderCount > 0)
				{
					var timeline = new DrawOrderTimeline(drawOrderCount);
					var slotCount = skeletonData.slots.Count;
					for (var i = 0; i < drawOrderCount; i++)
					{
						var offsetCount = input.ReadInt(true);
						var drawOrder = new int[slotCount];
						for (var ii = slotCount - 1; ii >= 0; ii--)
							drawOrder[ii] = -1;

						var unchanged = new int[slotCount - offsetCount];
						int originalIndex = 0, unchangedIndex = 0;
						for (var ii = 0; ii < offsetCount; ii++)
						{
							var slotIndex = input.ReadInt(true);
							// Collect unchanged items.
							while (originalIndex != slotIndex)
								unchanged[unchangedIndex++] = originalIndex++;
							// Set changed items.
							drawOrder[originalIndex + input.ReadInt(true)] = originalIndex++;
						}
						// Collect remaining unchanged items.
						while (originalIndex < slotCount)
							unchanged[unchangedIndex++] = originalIndex++;
						// Fill in unchanged items.
						for (var ii = slotCount - 1; ii >= 0; ii--)
							if (drawOrder[ii] == -1) drawOrder[ii] = unchanged[--unchangedIndex];
						timeline.setFrame(i, input.ReadSingle(), drawOrder);
					}
					timelines.Add(timeline);
					duration = Math.Max(duration, timeline.Frames[drawOrderCount - 1]);
				}

				// Event timeline.
				var eventCount = input.ReadInt(true);
				if (eventCount > 0)
				{
					var timeline = new EventTimeline(eventCount);
					for (var i = 0; i < eventCount; i++)
					{
						var time = input.ReadSingle();
						var eventData = skeletonData.events[input.ReadInt(true)];
						var evt = new Event(eventData)
						{
							Int = input.ReadInt(false),
							Float = input.ReadSingle(),
							String = input.ReadBoolean() ? input.ReadString() : eventData.String
						};
						timeline.setFrame(i, time, evt);
					}
					timelines.Add(timeline);
					duration = Math.Max(duration, timeline.Frames[eventCount - 1]);
				}
			}
			catch (IOException ex)
			{
				throw new SerializationException("Error reading skeleton file.", ex);
			}

			//		timelines.shrink();
			skeletonData.animations.Add(new Animation(name, timelines, duration));
		}

		private void ReadCurve(BinaryReader input, int frameIndex, CurveTimeline timeline)
		{
			switch (input.ReadByte())
			{
				case CurveStepped:
					timeline.SetStepped(frameIndex);
					break;
				case CurveBezier:
					SetCurve(timeline, frameIndex, input.ReadSingle(), input.ReadSingle(), input.ReadSingle(), input.ReadSingle());
					break;
			}
		}

		private void SetCurve(CurveTimeline timeline, int frameIndex, float cx1, float cy1, float cx2, float cy2)
		{
			timeline.SetCurve(frameIndex, cx1, cy1, cx2, cy2);
		}
	}
}
