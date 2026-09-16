package com.godot.game

import android.app.Application

/**
 * Stashes an Application-scoped Context reachable from plain static Kotlin/Java
 * code (UpdateInstaller) that Godot's C# side calls into via JavaClassWrapper,
 * without needing a reference to the current Activity. Registered as
 * android:name=".BeyondApp" on <application> in AndroidManifest.xml — this
 * doesn't touch or replace any of Godot's own Activity-based engine init.
 */
class BeyondApp : Application() {
    companion object {
        lateinit var instance: BeyondApp
            private set
    }

    override fun onCreate() {
        super.onCreate()
        instance = this
    }
}
