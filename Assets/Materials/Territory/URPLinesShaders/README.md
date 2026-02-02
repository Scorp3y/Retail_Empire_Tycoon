# URP Lines Shader

## Motivation

Create a Lines Shader based mainly in ShaderGraph that is easily modificable and understandable, that runs on Universal Render Pipeline.

## Shaders

This are the exported shaders on the current version:  
 - **Unlit Opaque Lines**  
 - **Unlit Transparent Lines**  
 - **Unlit Opaque Moving Lines**: Variation example with time-based moving lines. This is an educational mainly shader of a modification, the same result could be achieved using an Unity Animation.

## Properties

- **LineColor**: Color of the Line
- **BackgroundColor**: Color of the Background
- **LineSizeRatio**: Ratio of the line size over the total size. Example: a 0.1 ratio would mean 10% of the surface are lines and 90% background
- **Tiling**: Tiling of the resultant UV
- **Offset**: Offset of the resultant UV
- **Orientation**: Enum that determines how the UV is generated
	- _UV_: the UV is used from the Object UV
	- _X_: the lines are drawn over the X Axis
	- _Y_: the lines are drawn over the Y Axis
	- _Z_: the lines are drawn over the Z Axis
- **Normalized**: Boolean that normalizes the position when the orientation is an Axis. Used for Skybox mainly.
- **LocalPosition**: Boolean that uses local position when the orientation is an Axis instead of the world position.
- **AlphaClipTreshold**: Used only in transparent Shaders, determines the Alpha Clip Treshold.
- **Speed**: Used only in movingg lines Shaders, determines the Speed of the offset per second.