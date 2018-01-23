﻿using System;
using System.IO;

namespace olcEngineSpriteEditor
{
    public class Sprite
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public short[] Colours { get; private set; }
        public char[] Glyphs { get; private set; }

        public string File { get; private set; } = string.Empty;

        // Load Sprite from file
        public Sprite(string file)
        {
            File = file;

            Load();
        }

        public Sprite(int width, int height)
        {
            Width = width;
            Height = height;

            Colours = new short[Width * Height];

            for (var i = 0; i < Colours.Length; i++) Colours[i] = (short) ConsoleColor.Black;

            Glyphs = new char[Width * Height];
        }

        public short GetColour(int x, int y)
        {
            return x < 0 || x > Width || y < 0 || y > Height ? (short) 0 : Colours[y * Width + x];
        }

        public void SetColour(int x, int y, short color)
        {
            if (x < 0 || x > Width || y < 0 || y > Height)
                return;
            Colours[y * Width + x] = color;
        }

        public void Save()
        {
            Save(File);
        }

        public void Save(string file)
        {
            File = file;

            using (var writer = new BinaryWriter(System.IO.File.Open(file, FileMode.OpenOrCreate)))
            {
                writer.Write(Width);
                writer.Write(Height);

                for (var i = 0; i < Width * Height; i++)
                {
                    writer.Write(Colours[i]);
                }

                for (var i = 0; i < Width * Height; i++)
                {
                    writer.Write(Glyphs[i]);
                }
            }
        }

        private void Load()
        {
            using (var reader = new BinaryReader(System.IO.File.Open(File, FileMode.Open)))
            {
                Width = reader.ReadInt32();
                Height = reader.ReadInt32();
                Colours = new short[Width * Height];
                Glyphs = new char[Width * Height];

                for (var i = 0; i < Width * Height; i++)
                {
                    Colours[i] = reader.ReadInt16();
                }

                for (var i = 0; i < Width * Height; i++)
                {
                    Glyphs[i] = reader.ReadChar();
                }
            }
        }

        public override string ToString()
        {
            return $"Width: {Width} Height: {Height} " +
                   $"Colours: {string.Join(",", Colours)} " +
                   $"Glyphs: {string.Join(",", Glyphs)}";
        }
    }

}