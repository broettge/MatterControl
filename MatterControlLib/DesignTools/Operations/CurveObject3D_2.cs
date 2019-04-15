﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class CurveObject3D_2 : OperationSourceContainerObject3D, IEditorDraw
	{
		// this needs to serialize but not be editable (so public but not a get set)
		public Vector3 RotationOffset;

		public CurveObject3D_2()
		{
			Name = "Curve".Localize();
		}

		[DisplayName("Bend Up")]
		public bool BendCcw { get; set; } = true;

		public double Diameter { get; set; } = double.MinValue;

		[Range(3, 360, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Ensures the rotated part has a minimum number of sides per complete rotation")]
		public double MinSidesPerRotation { get; set; } = 10;

		[Range(0, 100, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		[Description("Where to start the bend as a percent of the width of the part")]
		public double StartPercent { get; set; } = 50;

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			if (layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				// we want to measure the
				var currentMatrixInv = Matrix.Inverted;
				var aabb = this.GetAxisAlignedBoundingBox(currentMatrixInv);

				layer.World.RenderCylinderOutline(this.WorldMatrix(), Vector3.Zero, Diameter, aabb.ZSize, 30, Color.Red);
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool propertyUpdated = Diameter == double.MinValue;
			if (StartPercent < 0
				|| StartPercent > 100)
			{
				StartPercent = Math.Min(100, Math.Max(0, StartPercent));
				propertyUpdated = true;
			}

			var originalAabb = this.GetAxisAlignedBoundingBox();

			var rebuildLocks = this.RebuilLockAll();

			return ApplicationController.Instance.Tasks.Execute(
				"Curve".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					this.Translate(-RotationOffset);
					SourceContainer.Visible = true;
					RemoveAllButSource();

					// remember the current matrix then clear it so the parts will rotate at the original wrapped position
					var currentMatrix = Matrix;
					Matrix = Matrix4X4.Identity;

					var aabb = this.GetAxisAlignedBoundingBox();
					if (Diameter == double.MinValue)
					{
						// uninitialized set to a reasonable value
						Diameter = (int)aabb.XSize;
						// TODO: ensure that the editor display value is updated
					}

					if (Diameter > 0)
					{
						var radius = Diameter / 2;
						var circumference = MathHelper.Tau * radius;
						var rotationCenter = new Vector3(aabb.MinXYZ.X + (aabb.MaxXYZ.X - aabb.MinXYZ.X) * (StartPercent / 100), aabb.MaxXYZ.Y + radius, aabb.Center.Z);
						double numRotations = aabb.XSize / circumference;
						double numberOfCuts = numRotations * MinSidesPerRotation;
						double cutSize = aabb.XSize / numberOfCuts;
						double cutPosition = aabb.MinXYZ.X + cutSize;
						var cuts = new List<double>();
						for (int i = 0; i < numberOfCuts; i++)
						{
							cuts.Add(cutPosition);
							cutPosition += cutSize;
						}

						RotationOffset = rotationCenter;
						if (!BendCcw)
						{
							// fix the stored center so we draw correctly
							RotationOffset.Y = aabb.MinXYZ.Y - radius;
						}

						foreach (var sourceItem in SourceContainer.VisibleMeshes())
						{
							var originalMesh = sourceItem.Mesh;
							var transformedMesh = originalMesh.Copy(CancellationToken.None);
							var itemMatrix = sourceItem.WorldMatrix(SourceContainer);

							if (!BendCcw)
							{
								// rotate around so it will bend correctly
								itemMatrix *= Matrix4X4.CreateTranslation(0, -aabb.MaxXYZ.Y, 0);
								itemMatrix *= Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
								itemMatrix *= Matrix4X4.CreateTranslation(0, aabb.MaxXYZ.Y - aabb.YSize, 0);
							}

							// transform into this space
							transformedMesh.Transform(itemMatrix);

							// split the mesh along the x axis
							SplitMeshAlongX(transformedMesh, cuts);

							for (int i = 0; i < transformedMesh.Vertices.Count; i++)
							{
								var position = transformedMesh.Vertices[i];

								var angleToRotate = ((position.X - rotationCenter.X) / circumference) * MathHelper.Tau - MathHelper.Tau / 4;
								var distanceFromCenter = rotationCenter.Y - position.Y;

								var rotatePosition = new Vector3Float(Math.Cos(angleToRotate), Math.Sin(angleToRotate), 0) * distanceFromCenter;
								rotatePosition.Z = position.Z;
								transformedMesh.Vertices[i] = rotatePosition + new Vector3Float(rotationCenter.X, radius + aabb.MaxXYZ.Y, 0);
							}

							// transform back into item local space
							transformedMesh.Transform(Matrix4X4.CreateTranslation(-RotationOffset) * itemMatrix.Inverted);

							transformedMesh.MarkAsChanged();
							transformedMesh.CalculateNormals();

							var newMesh = new Object3D()
							{
								Mesh = transformedMesh
							};
							newMesh.CopyWorldProperties(sourceItem, this, Object3DPropertyFlags.All);
							this.Children.Add(newMesh);
						}

						// set the matrix back
						Matrix = currentMatrix;
						this.Translate(new Vector3(RotationOffset));
						SourceContainer.Visible = false;
						rebuildLocks.Dispose();
					}

					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));

					return Task.CompletedTask;
				});
		}

		public static void SplitMeshAlongX(Mesh mesh, List<double> cuts)
		{
			var faceSplit = false;
			do
			{
				faceSplit = false;
				for(int i=0; i<mesh.Faces.Count; i++)
				{
					for (int j = 0; j < cuts.Count; j++)
					{
						if (mesh.SplitFace(i, new Plane(Vector3.UnitX, cuts[j]), .1))
						{
							faceSplit = true;
							i = mesh.Faces.Count;
							break;
						}
					}
				}
			} while (faceSplit);
			return;

			var result = new Mesh();
			var splitSections = new List<Mesh>();
			for (int i = 0; i < cuts.Count; i++)
			{
				// add a mesh to hold all the polygons that need to be split for each split section
				splitSections.Add(new Mesh());
			}

			var bounds = mesh.GetAxisAlignedBoundingBox();

			var vertices = mesh.Vertices;

			// add the face to every split section it crosses a side of
			foreach (var face in mesh.Faces)
			{
				var faceBounds = face.GetAxisAlignedBoundingBox(mesh);
				for (int i = 0; i < cuts.Count; i++)
				{
					// if the face right >= cuts[0] (left)
					// and face left <= cuts[1] (right)
					if (faceBounds.MaxXYZ.X <= cuts[i]
						&& faceBounds.MinXYZ.X >= cuts[i + 1])
					{
						var subMesh = splitSections[i];
						subMesh.CreateFace(new Vector3Float[] { vertices[face.v0], vertices[face.v1], vertices[face.v2] });
					}
				}
			}

			// foreach split section, cut all the polygons that cross the sides of it
			foreach (var sectionMesh in splitSections)
			{
				// find what cut needs to be done to each face
			}

			// union all the faces back into results

			// clean the result

			return;
		}
	}
}