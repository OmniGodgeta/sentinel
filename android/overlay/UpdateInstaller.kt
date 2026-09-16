package com.godot.game

import android.content.Intent
import androidx.core.content.FileProvider
import java.io.File

/**
 * Hands a downloaded update APK to the system package installer. Called from C#
 * (UpdateChecker.cs) via Godot's JavaClassWrapper.wrap("com.godot.game.UpdateInstaller")
 * — a plain static method, not a full Godot Android Plugin, since all it needs is
 * an Application Context (from BeyondApp) rather than the current Activity.
 *
 * apkPath is an absolute filesystem path under the app's own internal files dir
 * (Godot's user:// on Android). Reuses the FileProvider Godot's own godot-lib
 * .aar already declares at "<applicationId>.fileprovider" (its
 * godot_provider_paths.xml grants a files-path over the whole internal files
 * root) — no extra <provider>/paths XML of our own needed.
 */
object UpdateInstaller {
    @JvmStatic
    fun install(apkPath: String): Boolean {
        return try {
            val ctx = BeyondApp.instance.applicationContext
            val file = File(apkPath)
            if (!file.exists()) return false

            val authority = ctx.packageName + ".fileprovider"
            val uri = FileProvider.getUriForFile(ctx, authority, file)

            val intent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(uri, "application/vnd.android.package-archive")
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            ctx.startActivity(intent)
            true
        } catch (e: Exception) {
            false
        }
    }
}
