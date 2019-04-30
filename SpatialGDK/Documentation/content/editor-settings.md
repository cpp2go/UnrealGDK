<%(TOC)%>
# GDK Editor Settings

Use Editor Settings as advanced configurations to configure a local deployment, and schema and snapshot generation.

The following use cases show when you use Editor Settings to configure the properties:

- When your game world is larger than the default setting of 2km*2km, you should modify the values of simulation dimensions for the launch configuration file. Alternatively, write and upload your own launch configuration file.
- When you are testing your multiserver simulation, to configure the grid size, you might want to specify rectangle grid column count and row count. Alternatively, specify them in your own launch configuration file and upload the file.

> **Note**: You can find all the settings that you configure using Editor Settings in the `DefaultSpatialGDKEditorSettings.ini` file from the `<GameRoot>\Config\` directory.

To learn more about all the properties available in the **Editor Settings** panel, check the following table:

<table>
<tbody>
<tr>
<td><strong>Sections</strong></td>
<td><strong>Properties</strong></td>
<td><strong>Description</strong></td>
</tr>
<tr>
<td>General</td>
<td><span style="font-weight: 400;">SpatialOS directory</span></td>
<td><span style="font-weight: 400;">The directory for SpatialOS-related files, for example, <code>C:/Projects/MyGame/spatial/</code>.</td>
</tr>
<tr>
<td>Play in Editor Settings</td>
<td>Delete dynamically spawned entities</td>
<td>
<p>Decide whether to delete all the dynamically spawned entities when server workers disconnect. By default, the check box is selected.</p>
<ul>
<li>If you select it, when you restart <strong>Play In Editor</strong>, the game is started from a clean state.</li>
<li>If you deselect it, when you restart <strong>Play In Editor</strong>, the game reconnects to a live deployment with entities from the previous session present.</li>
</ul>
</td>
</tr>
<tr>
<td rowspan="5">Launch</td>
<td>Generate launch configuration file</td>
<td>
<p>Decide whether to auto-generate a launch configuration file when you launch your project through the toolbar. By default, the check box is selected.</span></p>
<ul>
<li>If you select it, you can specify the properties in the <strong>Launch configuration file description</strong> section.</span></li>
<li>If you deselect it, you must choose your own launch configuration file in the <strong>Upload launch configuration file</strong> field.</li>
</ul>
</td>
</tr>
<tr>
<td>Upload launch configuration file</td>
<td>
  <p>The launch configuration file used for <code>spatial local launch</code>, for example, <code>C:/Projects/MyGame/spatial/default_launch.json</code>.</p>
<p><span style="font-weight: 400;"><strong>Note</strong>: This field is valid only when <strong>Generate launch configuration file</strong> is deselected.</span></p>
</td>
</tr>
<tr>
<td>Stop local launch on exit</td>
<td>
<p>Decide whether to stop <code>spatial local launch</code> when you shut down Unreal Editor. By default, the check box is deselected.</p>
</td>
</tr>
<tr>
<td>Command line flags for local launch</td>
<td>
<p>The command line flags passed to <code>spatial local launch</code>.</p>
<p><strong>Tip</strong>: To check available flags, open the CLI and run <code>spatial local launch --help</code>. For example, to connect to the local deployment from a different machine on the local network, add the <code>--runtime_ip</code> flag.</p>
</td>
</tr>
<tr>
<td>Launch configuration file description</td>
<td>
<p>The properties for the launch configuration file.</p>
<p><strong>Note</strong>: The fields in this section are valid only when you select <strong>Generate launch configuration file</strong>. For information about the definition of each property, see <a href="https://docs.improbable.io/reference/13.7/shared/project-layout/launch-config">Launch configuration file</a>.</p>
</td>
</tr>
<tr>
<td rowspan="3">Snapshots</td>
<td>Snapshot directory</td>
<td>The directory for your SpatialOS snapshot, for example, <code>C:/Projects/MyGame/spatial/snapshots/</code>.</td>
</tr>
<tr>
<td>Snapshot file name</td>
<td>The name of your SpatialOS snapshot file, for example, <code>default.snapshot</code>.</td>
</tr>
<tr>
<td>Generate placeholder entities in snapshot</td>
<td>
<p>Decide whether to add placeholder entities to the snapshot on generation. By default, the check box is selected.</span></p>
<p>If you select it, you can see these entities in the Inspector, which shows the areas that a server-worker instance has authority over.</p>
</td>
</tr>
<tr>
<td>Schema</td>
<td>Generated schema directory</td>
<td>The directory that stores the generated schema files, for example, <code>C:/Projects/MyGame/spatial/schema/unreal/generated/</code>.</td>
</tr>
</tbody>
</table>