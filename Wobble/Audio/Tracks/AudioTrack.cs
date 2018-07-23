﻿using System;
using System.IO;
using ManagedBass;
using ManagedBass.Fx;

namespace Wobble.Audio.Tracks
{
    /// <summary>
    ///     This playable audio track should be used for bigger audio files such as songs.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage
    public class AudioTrack : IPlayableAudio, IDisposable
    {
        /// <summary>
        ///     The currently loaded audio stream, if there is one.
        /// </summary>
        public int Stream { get; private set; }

        /// <summary>
        ///     The length of the current audio stream in milliseconds.
        /// </summary>
        public double Length
        {
            get
            {
                if (!StreamLoaded || IsDisposed)
                    throw new InvalidOperationException("Cannot get track length if disposed or stream not loaded");

                return Bass.ChannelBytes2Seconds(Stream, Bass.ChannelGetLength(Stream)) * 1000;
            }
        }

        /// <summary>
        ///     The position of the current audio stream in milliseconds (from BASS library)
        /// </summary>
        public double Position
        {
            get
            {
                if (!StreamLoaded || IsDisposed)
                    throw new InvalidOperationException("Cannot get track position if disposed or stream not loaded");

                return Bass.ChannelBytes2Seconds(Stream, Bass.ChannelGetPosition(Stream)) * 1000;
            }
        }

        /// <summary>
        ///     The true position of the audio, taking into frame times. Use this for more accurate
        ///     results (such as for rhythm games, or things where the audio time really matters)
        /// </summary>
        public double Time { get; private set; }

        /// <summary>
        ///     If the stream is currently loaded.
        /// </summary>
        public bool StreamLoaded => Stream != 0;

        /// <summary>
        ///     If the audio stream has already been disposed of.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        ///     If the audio track has already been played.
        /// </summary>
        public bool HasPlayed { get; private set; }

        /// <summary>
        ///     Returns if the audio stream is currently playing.
        /// </summary>
        public bool IsPlaying => Bass.ChannelIsActive(Stream) == PlaybackState.Playing;

        /// <summary>
        ///     Returns if the audio stream is currently paused
        /// </summary>
        public bool IsPaused => Bass.ChannelIsActive(Stream) == PlaybackState.Paused;

        /// <summary>
        ///     Returns if the audio stream is currently stopped.
        /// </summary>
        public bool IsStopped => Bass.ChannelIsActive(Stream) == PlaybackState.Stopped;

        /// <summary>
        ///     If the audio track is currently pitched based on the rate.
        /// </summary>
        public bool IsPitched { get; private set; } = true;

        /// <summary>
        ///     If the audio track has played and is now stopped.
        /// </summary>
        public bool IsLeftOver => HasPlayed && IsStopped;

        /// <summary>
        ///     The rate at which the audio plays at.
        /// </summary>
        private float _rate = 1.0f;
        public float Rate
        {
            get => _rate;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Cannot set rate to 0 or below.");

                _rate = value;
                Bass.ChannelSetAttribute(Stream, ChannelAttribute.Tempo, _rate * 100 - 100);

                // When the rate changes, we also want to set the pitching again with the new rate.
                ToggleRatePitching(IsPitched);
            }
        }

        /// <summary>
        ///    Loads an audio track from a file.
        /// </summary>
        /// <param name="path"></param>
        public AudioTrack(string path)
        {
            Stream = Bass.CreateStream(path, Flags: BassFlags.Decode);
            AfterLoad();
        }

        /// <summary>
        ///     Loads an audio track from a byte array
        /// </summary>
        /// <param name="data"></param>
        public AudioTrack(byte[] data)
        {
            Stream = Bass.CreateStream(data, 0, data.Length, BassFlags.Decode);
            AfterLoad();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void Play()
        {
            CheckIfDisposed();

            if (IsPlaying)
                throw new AudioEngineException("Cannot play track if it is already playing.");

            // If the track has never played before, we'll want to set its rate pitching.
            if (!HasPlayed)
                ToggleRatePitching(IsPitched);

            Bass.ChannelPlay(Stream);
            HasPlayed = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void Pause()
        {
            CheckIfDisposed();

            if (!IsPlaying || IsPaused)
                throw new AudioEngineException("Cannot pause audio track if it is not playing or if already paused.");

            Bass.ChannelPause(Stream);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public void Stop()
        {
            CheckIfDisposed();
            Bass.ChannelStop(Stream);
            Dispose();
        }

        /// <summary>
        ///     Restarts the track from the beginning.
        /// </summary>
        public void Restart()
        {
            CheckIfDisposed();

            if (IsPlaying)
                Pause();

            Seek(0);
            Play();
        }

        /// <summary>
        ///     Seeks to a position in the track
        /// </summary>
        /// <param name="pos"></param>
        public void Seek(double pos)
        {
            CheckIfDisposed();

            if (pos > Length || pos < -1)
                throw new AudioEngineException("You can only seek to a position greater than -1 and below its length.");

            Bass.ChannelSetPosition(Stream, Bass.ChannelSeconds2Bytes(Stream, pos / 1000d));
        }

        /// <summary>
        ///     Toggles the pitching for the audio track.
        ///     Used if you want to have songs pitched when the rate changes.
        /// </summary>
        /// <param name="shouldPitch"></param>
        public void ToggleRatePitching(bool shouldPitch)
        {
            CheckIfDisposed();

            IsPitched = shouldPitch;

            if (IsPitched)
                Bass.ChannelSetAttribute(Stream, ChannelAttribute.Pitch, Math.Log(Math.Pow(Rate, 12), 2));
            else
                Bass.ChannelSetAttribute(Stream, ChannelAttribute.Pitch, 0);
        }

        /// <summary>
        ///     Corrects the true time of the track with the actual passed frame time.
        /// </summary>
        /// <param name="timeSinceLastFrame"></param>
        internal void CorrectTime(double timeSinceLastFrame)
        {
            if (!StreamLoaded || IsStopped)
            {
                Time = 0;
                return;
            }

            if (!IsPlaying)
            {
                Time = Position;
                return;
            }

            Time = (Position + (Time + timeSinceLastFrame * Rate)) / 2;
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException"></exception>
        public void Dispose()
        {
            Bass.StreamFree(Stream);
            Stream = 0;
            IsDisposed = true;

            AudioManager.Tracks.Remove(this);
        }

        /// <summary>
        ///     After initially loading the stream, it is advisable to call this to do any further initialization.
        /// </summary>
        /// <exception cref="AudioEngineException"></exception>
        private void AfterLoad()
        {
            if (!StreamLoaded)
                throw new AudioEngineException("Cannot call AfterLoad if stream isn't loaded.");

            AudioManager.Tracks.Add(this);

            Stream = BassFx.TempoCreate(Stream, BassFlags.FxFreeSource);
            Bass.ChannelAddFlag(Stream, BassFlags.AutoFree);
        }

        /// <summary>
        ///    Checks if the audio stream is disposed and/or unloaded.
        /// </summary>
        private void CheckIfDisposed()
        {
            if (!StreamLoaded || IsDisposed)
                throw new PlayableAudioDisposedException("You cannot change a disposed/unloaded track's position.");
        }
    }
}